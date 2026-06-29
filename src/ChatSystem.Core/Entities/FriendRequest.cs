using ChatSystem.Core.Enums;

namespace ChatSystem.Core.Entities;

public class FriendRequest
{
    public int Id { get; set; }

    public int FromUserId { get; set; }

    public int ToUserId { get; set; }

    public FriendRequestStatus Status { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? HandledAt { get; set; }
}
