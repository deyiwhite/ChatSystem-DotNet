namespace ChatSystem.Core.Entities;

public class Friend
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public int FriendId { get; set; }

    public DateTime CreatedAt { get; set; }
}
