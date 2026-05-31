namespace MarketingApp.Application.DTOs;

public record AdminUserDto
{
    public Guid Id { get; init; }
    public string FullName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string Role { get; init; } = "user";
    public bool IsActive { get; init; }
    public Guid? SmtpGroupId { get; init; }
    public string? SmtpGroupName { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
    // High-level signature info (admin can see if user has set personal signature)
    public bool HasPersonalSignature { get; init; }
}

public record CreateUserDto
{
    public string FullName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public string Role { get; init; } = "user"; // "user" | "admin"
    public Guid? SmtpGroupId { get; init; }
}

public record UpdateUserDto
{
    public string FullName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public Guid? SmtpGroupId { get; init; }
    public bool IsActive { get; init; } = true;
}

public record ChangeRoleDto
{
    public string Role { get; init; } = "user"; // "user" | "admin"
}

public record ResetPasswordDto
{
    public string NewPassword { get; init; } = string.Empty;
}
