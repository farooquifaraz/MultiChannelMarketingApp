using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MarketingApp.Application.DTOs;
using MarketingApp.Application.Interfaces;
using System.Security.Claims;

namespace MarketingApp.API.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("register")]
    [ProducesResponseType(typeof(ApiResponse<AuthResponseDto>), 201)]
    public async Task<IActionResult> Register([FromBody] RegisterDto dto, CancellationToken ct)
    {
        var result = await _authService.RegisterAsync(dto, ct);
        return StatusCode(201, ApiResponse<AuthResponseDto>.Ok(result, "Registration successful"));
    }

    [HttpPost("login")]
    [ProducesResponseType(typeof(ApiResponse<AuthResponseDto>), 200)]
    public async Task<IActionResult> Login([FromBody] LoginDto dto, CancellationToken ct)
    {
        var result = await _authService.LoginAsync(dto, ct);
        return Ok(ApiResponse<AuthResponseDto>.Ok(result, "Login successful"));
    }

    [HttpPost("refresh")]
    [ProducesResponseType(typeof(ApiResponse<AuthResponseDto>), 200)]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenDto dto, CancellationToken ct)
    {
        var result = await _authService.RefreshTokenAsync(dto.RefreshToken, ct);
        return Ok(ApiResponse<AuthResponseDto>.Ok(result));
    }

    [Authorize]
    [HttpPost("logout")]
    [ProducesResponseType(typeof(ApiResponse), 200)]
    public async Task<IActionResult> Logout([FromBody] RefreshTokenDto dto, CancellationToken ct)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        await _authService.LogoutAsync(userId, dto.RefreshToken, ct);
        return Ok(ApiResponse<object>.Ok(null!, "Logged out successfully"));
    }
}
