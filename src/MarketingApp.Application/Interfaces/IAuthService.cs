using MarketingApp.Application.DTOs;

namespace MarketingApp.Application.Interfaces;

public interface IAuthService
{
    Task<AuthResponseDto> RegisterAsync(RegisterDto dto, CancellationToken ct = default);
    Task<AuthResponseDto> LoginAsync(LoginDto dto, CancellationToken ct = default);
    Task<AuthResponseDto> RefreshTokenAsync(string refreshToken, CancellationToken ct = default);
    Task LogoutAsync(Guid userId, string refreshToken, CancellationToken ct = default);

    /// <summary>Generate a reset token and email a reset link. Always succeeds silently (never reveals
    /// whether the email exists) to avoid account enumeration.</summary>
    Task ForgotPasswordAsync(string email, CancellationToken ct = default);

    /// <summary>Validate the reset token + set the new password.</summary>
    Task ResetPasswordAsync(string email, string token, string newPassword, CancellationToken ct = default);
}
