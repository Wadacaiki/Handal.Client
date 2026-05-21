namespace Handal.Client.Models;

/// <summary>
/// Статус модерации тега или его привязки к лоту
/// </summary>
public enum ModerationStatus
{
    Pending,
    Approved,
    Rejected
}

/// <summary>
/// Тег в общем каталоге тегов платформы
/// </summary>
public class TagDefinition
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string NormalizedName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public ModerationStatus Status { get; set; } = ModerationStatus.Pending;
    public string CreatedByUserId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public string? ModeratedByAdminId { get; set; }
    public DateTime? ModeratedAt { get; set; }
    public string? RejectReason { get; set; }
    public int UsageCount { get; set; } = 0;
}

/// <summary>
/// Привязка тега к конкретному лоту
/// </summary>
public class AuctionTagAssignment
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string AuctionId { get; set; } = string.Empty;
    public string TagDefinitionId { get; set; } = string.Empty;
    public string RawInput { get; set; } = string.Empty;
    public string NormalizedTag { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public ModerationStatus Status { get; set; } = ModerationStatus.Pending;
    public string CreatedByUserId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public string? ModeratedByAdminId { get; set; }
    public DateTime? ModeratedAt { get; set; }
    public string? RejectReason { get; set; }
}

/// <summary>
/// Результат добавления тега к лоту
/// </summary>
public class AuctionTagAttachResult
{
    public bool Success { get; set; }
    public bool UsedExistingApprovedTag { get; set; }
    public bool CreatedNewTagDefinition { get; set; }
    public string? Error { get; set; }
    public TagDefinition? TagDefinition { get; set; }
    public AuctionTagAssignment? Assignment { get; set; }
}

public class TagModerationLogEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string AdminId { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string TargetTagName { get; set; } = string.Empty;
    public string? AuctionId { get; set; }
    public string? Reason { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.Now;
}
