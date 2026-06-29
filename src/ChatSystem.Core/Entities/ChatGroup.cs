namespace ChatSystem.Core.Entities;

public class ChatGroup
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public int OwnerId { get; set; }

    public DateTime CreatedAt { get; set; }
}
