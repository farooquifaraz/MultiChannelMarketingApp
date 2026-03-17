namespace MarketingApp.Application.DTOs;

public record NotificationDto
{
    public Guid Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string Type { get; init; } = "info";
    public string? RelatedEntity { get; init; }
    public Guid? RelatedEntityId { get; init; }
    public bool IsRead { get; init; }
    public DateTime CreatedAt { get; init; }
}

public record NotificationCountDto
{
    public int TotalUnread { get; init; }
    public int InfoCount { get; init; }
    public int SuccessCount { get; init; }
    public int WarningCount { get; init; }
    public int ErrorCount { get; init; }
}
