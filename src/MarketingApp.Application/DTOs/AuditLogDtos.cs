namespace MarketingApp.Application.DTOs;

public record AuditLogDto
{
    public Guid Id { get; init; }
    public Guid? UserId { get; init; }
    public string? UserFullName { get; init; }
    public string? UserEmail { get; init; }
    public string Action { get; init; } = string.Empty;
    public string? Entity { get; init; }
    public Guid? EntityId { get; init; }
    public string? Details { get; init; }
    public string? IpAddress { get; init; }
    public DateTime CreatedAt { get; init; }
}

public record AuditLogQuery
{
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 25;
    public Guid? UserId { get; init; }
    public string? Action { get; init; }
    public string? Entity { get; init; }
    public DateTime? FromDate { get; init; }
    public DateTime? ToDate { get; init; }
    public string? Search { get; init; }
}
