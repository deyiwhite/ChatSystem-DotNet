using ChatSystem.Core.Enums;

namespace ChatSystem.Core.Entities;

public class GroupMessage
{
    public int Id { get; set; }

    public int GroupId { get; set; }

    public int FromUserId { get; set; }

    public string Content { get; set; } = string.Empty;

    public MessageType Type { get; set; }

    public string? AttachmentFileName { get; set; }

    public string? AttachmentStoredName { get; set; }

    public long? AttachmentSize { get; set; }

    public DateTime SentAt { get; set; }

    public bool IsDeletedByAdmin { get; set; }
}
