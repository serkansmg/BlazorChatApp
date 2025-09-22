using BlazorChatApp.Models.Identity;

public class FriendRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SenderId { get; set; }
    public Guid ReceiverId { get; set; }
    public FriendRequestStatus Status { get; set; } = FriendRequestStatus.Pending;
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public DateTime? RespondedAt { get; set; }
    public string? Message { get; set; } // İlk mesaj
    
    public virtual AppUser Sender { get; set; } = null!;
    public virtual AppUser Receiver { get; set; } = null!;
}

public enum FriendRequestStatus
{
    Pending,
    Accepted,
    Rejected
}