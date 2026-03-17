using MarketingApp.Application.DTOs;

namespace MarketingApp.Application.Interfaces;

public interface IAuthService
{
    Task<AuthResponseDto> RegisterAsync(RegisterDto dto, CancellationToken ct = default);
    Task<AuthResponseDto> LoginAsync(LoginDto dto, CancellationToken ct = default);
    Task<AuthResponseDto> RefreshTokenAsync(string refreshToken, CancellationToken ct = default);
    Task LogoutAsync(Guid userId, string refreshToken, CancellationToken ct = default);
}
