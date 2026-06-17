using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using MarketingApp.Application.DTOs;
using MarketingApp.Application.Interfaces;
using MarketingApp.Domain.Entities;
using MarketingApp.Domain.Exceptions;

namespace MarketingApp.Application.Services;

public class AuthService : IAuthService
{
    private readonly IGenericRepository<User> _userRepo;
    private readonly IGenericRepository<RefreshToken> _refreshTokenRepo;
    private readonly IConfiguration _config;
    private readonly IEmailService _emailService;
    private readonly ISmtpGroupService _smtpGroups;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IGenericRepository<User> userRepo,
        IGenericRepository<RefreshToken> refreshTokenRepo,
        IConfiguration config,
        IEmailService emailService,
        ISmtpGroupService smtpGroups,
        ILogger<AuthService> logger)
    {
        _userRepo = userRepo;
        _refreshTokenRepo = refreshTokenRepo;
        _config = config;
        _emailService = emailService;
        _smtpGroups = smtpGroups;
        _logger = logger;
    }

    public async Task<AuthResponseDto> RegisterAsync(RegisterDto dto, CancellationToken ct = default)
    {
        var normalizedEmail = dto.Email.ToLowerInvariant();
        var exists = await _userRepo.AnyAsync(u => u.Email == normalizedEmail, ct);
        if (exists)
            throw new ConflictException("A user with this email already exists.");

        // BUG-001 fix: the FIRST user on a fresh deployment becomes admin automatically.
        // Without this, a fresh prod deploy ends up with zero admins and the Settings /
        // SmtpGroups / Audit Logs / Admin Users pages are unreachable except via direct
        // DB intervention. Industry-standard pattern for self-hosted SaaS first-run.
        var anyUserAlready = await _userRepo.AnyAsync(u => true, ct);
        var role = anyUserAlready ? "user" : "admin";

        var user = new User
        {
            FullName = dto.FullName,
            Email = normalizedEmail,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password, workFactor: 12),
            Role = role,
            // P2.4 — every new user joins the seeded Legacy org by default so org_id is never null.
            // (Self-serve "new org per signup" is a later slice; this keeps behaviour identical today.)
            OrganizationId = Organization.LegacyOrgId
        };

        await _userRepo.AddAsync(user, ct);
        _logger.LogInformation("User registered: {Email} as role={Role}", user.Email, role);

        return await GenerateAuthResponse(user, ct);
    }

    public async Task<AuthResponseDto> LoginAsync(LoginDto dto, CancellationToken ct = default)
    {
        var users = await _userRepo.FindAsync(u => u.Email == dto.Email.ToLowerInvariant(), ct);
        var user = users.FirstOrDefault();

        if (user == null || !BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
            throw new MarketingApp.Domain.Exceptions.AppValidationException("Invalid email or password.");

        if (!user.IsActive)
            throw new ForbiddenException("Your account has been deactivated.");

        _logger.LogInformation("User logged in: {Email}", user.Email);
        return await GenerateAuthResponse(user, ct);
    }

    public async Task<AuthResponseDto> RefreshTokenAsync(string refreshToken, CancellationToken ct = default)
    {
        var tokens = await _refreshTokenRepo.FindAsync(t => t.Token == refreshToken && !t.IsRevoked, ct);
        var token = tokens.FirstOrDefault();

        if (token == null || token.ExpiresAt < DateTime.UtcNow)
            throw new MarketingApp.Domain.Exceptions.AppValidationException("Invalid or expired refresh token.");

        token.IsRevoked = true;
        await _refreshTokenRepo.UpdateAsync(token, ct);

        var user = await _userRepo.GetByIdAsync(token.UserId, ct)
            ?? throw new NotFoundException("User", token.UserId);

        return await GenerateAuthResponse(user, ct);
    }

    public async Task LogoutAsync(Guid userId, string refreshToken, CancellationToken ct = default)
    {
        var tokens = await _refreshTokenRepo.FindAsync(t => t.UserId == userId && t.Token == refreshToken, ct);
        var token = tokens.FirstOrDefault();
        if (token != null)
        {
            token.IsRevoked = true;
            await _refreshTokenRepo.UpdateAsync(token, ct);
        }
    }

    public async Task ForgotPasswordAsync(string email, CancellationToken ct = default)
    {
        var normalizedEmail = (email ?? string.Empty).Trim().ToLowerInvariant();
        var user = (await _userRepo.FindAsync(u => u.Email == normalizedEmail, ct)).FirstOrDefault();

        // Never reveal whether the email exists — just return.
        if (user is null || !user.IsActive)
        {
            _logger.LogInformation("[ForgotPassword] No active user for {Email} — silently ignoring.", normalizedEmail);
            return;
        }

        // 6-digit numeric code (crypto-random), valid for 10 minutes. We store only its BCrypt hash.
        var code = System.Security.Cryptography.RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
        user.PasswordResetToken = BCrypt.Net.BCrypt.HashPassword(code, workFactor: 12);
        user.PasswordResetTokenExpiresAt = DateTime.UtcNow.AddMinutes(10);
        user.PasswordResetAttempts = 0;
        user.UpdatedAt = DateTime.UtcNow;
        await _userRepo.UpdateAsync(user, ct);

        var html = BuildResetCodeEmailHtml(user.FullName, code);

        // The simple SendAsync(...) overload is a no-op mock — real delivery needs provider settings.
        // Resolve the user's assigned SmtpGroup (or the platform default) and send through it.
        var group = await _smtpGroups.ResolveForUserAsync(user.Id, ct);
        if (group is null)
        {
            _logger.LogError("[ForgotPassword] No SmtpGroup configured (user or platform default) — cannot send reset email to {Email}. Set a default SMTP group in Admin → SMTP Groups.", user.Email);
            return;
        }

        var settings = SmtpGroupService.ToUserSmtpSettings(group);
        try
        {
            var ok = await _emailService.SendWithUserSettingsAsync(user.Email, "Reset your password", html, settings, ct);
            if (ok) _logger.LogInformation("[ForgotPassword] Reset email sent to {Email} via group {Group}.", user.Email, group.Name);
            else _logger.LogError("[ForgotPassword] Reset email send returned false for {Email} via group {Group}.", user.Email, group.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ForgotPassword] Failed to send reset email to {Email} via group {Group}.", user.Email, group.Name);
        }
    }

    private const int MaxResetAttempts = 5;

    public async Task ResetPasswordAsync(string email, string code, string newPassword, CancellationToken ct = default)
    {
        var normalizedEmail = (email ?? string.Empty).Trim().ToLowerInvariant();
        var user = (await _userRepo.FindAsync(u => u.Email == normalizedEmail, ct)).FirstOrDefault();

        // No active code / expired → generic invalid message (don't reveal which part failed).
        if (user is null
            || string.IsNullOrEmpty(user.PasswordResetToken)
            || user.PasswordResetTokenExpiresAt is null
            || user.PasswordResetTokenExpiresAt < DateTime.UtcNow)
        {
            throw new MarketingApp.Domain.Exceptions.AppValidationException(
                "This code is invalid or has expired. Please request a new one.");
        }

        // Too many wrong tries → burn the code (brute-force guard).
        if (user.PasswordResetAttempts >= MaxResetAttempts)
        {
            user.PasswordResetToken = null;
            user.PasswordResetTokenExpiresAt = null;
            await _userRepo.UpdateAsync(user, ct);
            throw new MarketingApp.Domain.Exceptions.AppValidationException(
                "Too many incorrect attempts. Please request a new code.");
        }

        if (!BCrypt.Net.BCrypt.Verify((code ?? string.Empty).Trim(), user.PasswordResetToken))
        {
            user.PasswordResetAttempts += 1;
            await _userRepo.UpdateAsync(user, ct);
            var left = MaxResetAttempts - user.PasswordResetAttempts;
            throw new MarketingApp.Domain.Exceptions.AppValidationException(
                left > 0 ? $"Incorrect code. {left} attempt(s) left." : "Too many incorrect attempts. Please request a new code.");
        }

        // Correct code → set new password, invalidate the code.
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword, workFactor: 12);
        user.PasswordResetToken = null;
        user.PasswordResetTokenExpiresAt = null;
        user.PasswordResetAttempts = 0;
        user.UpdatedAt = DateTime.UtcNow;
        await _userRepo.UpdateAsync(user, ct);
        _logger.LogInformation("[ResetPassword] Password reset for {Email}.", user.Email);
    }

    private static string BuildResetCodeEmailHtml(string name, string code) => $@"
<div style=""font-family:Arial,Helvetica,sans-serif;max-width:520px;margin:0 auto;color:#2b2f33;"">
  <h2 style=""color:#4f46e5;"">Your password reset code</h2>
  <p>Hi {System.Net.WebUtility.HtmlEncode(name)},</p>
  <p>Use the code below to reset your password. It expires in <strong>10 minutes</strong>.</p>
  <p style=""text-align:center;margin:28px 0;"">
    <span style=""display:inline-block;background:#eef2ff;color:#312e81;font-size:32px;font-weight:700;letter-spacing:8px;padding:14px 28px;border-radius:10px;"">{code}</span>
  </p>
  <p style=""font-size:13px;color:#5b6166;"">Enter this code on the reset page along with your new password.</p>
  <p style=""font-size:13px;color:#5b6166;"">If you didn't request this, you can safely ignore this email — your password won't change.</p>
</div>";

    private async Task<AuthResponseDto> GenerateAuthResponse(User user, CancellationToken ct)
    {
        var jwtSettings = _config.GetSection("Jwt");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings["SecretKey"]!));
        var expiryMinutes = int.Parse(jwtSettings["ExpiryMinutes"] ?? "60");

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Name, user.FullName),
            new Claim(ClaimTypes.Role, user.Role),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddMinutes(expiryMinutes),
            Issuer = jwtSettings["Issuer"],
            Audience = jwtSettings["Audience"],
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256Signature)
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var securityToken = tokenHandler.CreateToken(tokenDescriptor);
        var accessToken = tokenHandler.WriteToken(securityToken);

        var refreshTokenEntity = new RefreshToken
        {
            UserId = user.Id,
            Token = Convert.ToBase64String(Guid.NewGuid().ToByteArray()) + Convert.ToBase64String(Guid.NewGuid().ToByteArray()),
            ExpiresAt = DateTime.UtcNow.AddDays(int.Parse(jwtSettings["RefreshTokenExpiryDays"] ?? "30"))
        };
        await _refreshTokenRepo.AddAsync(refreshTokenEntity, ct);

        return new AuthResponseDto
        {
            AccessToken = accessToken,
            RefreshToken = refreshTokenEntity.Token,
            ExpiresAt = tokenDescriptor.Expires.Value,
            User = new UserDto
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email,
                Role = user.Role
            }
        };
    }
}
