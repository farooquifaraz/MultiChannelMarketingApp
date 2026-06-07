namespace MarketingApp.Application.DTOs;

/// <summary>An organization / tenant (P2.4). Read shape for the admin org list + "my org".</summary>
public class OrganizationDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public Guid? OwnerUserId { get; set; }
    public string PlanCode { get; set; } = "free";
    public bool IsActive { get; set; } = true;
    public int UserCount { get; set; }
    public bool IsLegacy { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>Create a new organization (admin). Slug auto-derived from Name when omitted.</summary>
public class CreateOrganizationDto
{
    public string Name { get; set; } = string.Empty;
    public string? Slug { get; set; }
    public string? PlanCode { get; set; }
}

/// <summary>Rename / re-plan an organization (admin). Slug stays stable.</summary>
public class UpdateOrganizationDto
{
    public string Name { get; set; } = string.Empty;
    public string? PlanCode { get; set; }
}

/// <summary>Move a user into an organization (admin).</summary>
public class AssignUserToOrgDto
{
    public Guid UserId { get; set; }
    public Guid OrganizationId { get; set; }
}
