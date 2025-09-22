namespace BlazorChatApp.Models.Identity;

public class GroupMember
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GroupId { get; set; }
    public Guid UserId { get; set; }
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
    public bool IsAdmin { get; set; } = false;
    public DateTime? LastReadAt { get; set; }
    
    // Navigation properties
    public virtual Group Group { get; set; } = null!;
    public virtual AppUser User { get; set; } = null!;
}