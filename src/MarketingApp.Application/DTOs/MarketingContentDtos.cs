namespace MarketingApp.Application.DTOs;

/// <summary>Request: a raw property / product brief to turn into multi-channel marketing copy (P3.6).</summary>
public class GenerateContentDto
{
    public string Brief { get; set; } = string.Empty;
    /// <summary>Optional channel focus: "all" (default) | "whatsapp" | "instagram" | "email".</summary>
    public string? Channel { get; set; }
}

public class WhatsAppContentDto
{
    public string Broadcast { get; set; } = string.Empty;
    public string StatusText { get; set; } = string.Empty;
}

public class InstagramContentDto
{
    public string Caption { get; set; } = string.Empty;
    public string ReelsHook { get; set; } = string.Empty;
    public string StoryCta { get; set; } = string.Empty;
}

public class EmailContentDto
{
    public string Subject { get; set; } = string.Empty;
    public string Preview { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
}

/// <summary>AI-generated multi-channel marketing copy (mirrors the Python hub's structure).</summary>
public class MarketingContentDto
{
    public WhatsAppContentDto WhatsApp { get; set; } = new();
    public InstagramContentDto Instagram { get; set; } = new();
    public EmailContentDto Email { get; set; } = new();
    /// <summary>An image prompt the user can paste into the Banner Studio.</summary>
    public string ImagePrompt { get; set; } = string.Empty;
    /// <summary>Which AI provider produced it.</summary>
    public string Provider { get; set; } = string.Empty;
}
