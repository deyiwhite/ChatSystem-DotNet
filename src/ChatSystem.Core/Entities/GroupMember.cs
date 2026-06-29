namespace ChatSystem.Core.Entities;

public class GroupMember
{
    public int Id { get; set; }

    public int GroupId { get; set; }

    public int UserId { get; set; }

    public DateTime JoinedAt { get; set; }
}
