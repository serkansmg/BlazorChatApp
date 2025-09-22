using BlazorChatApp.Models.Chat;

namespace BlazorChatApp.Models.Identity;

public class Group
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string AvatarUrl { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid CreatedById { get; set; }
    
    // Navigation properties
    public virtual AppUser CreatedBy { get; set; } = null!;
    public virtual ICollection<GroupMember> Members { get; set; } = new List<GroupMember>();
    public virtual ICollection<ChatMessageModel> Messages { get; set; } = new List<ChatMessageModel>();
}