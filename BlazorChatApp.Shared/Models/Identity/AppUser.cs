using Microsoft.AspNetCore.Identity;
using BlazorChatApp.Models.Chat;

namespace BlazorChatApp.Models.Identity;

public class AppUser : IdentityUser<Guid>
{
    // Chat için ekstra özellikler
    public string Name { get; set; }
    public string Surname { get; set; }
    
    public string DisplayName => $"{Name} {Surname}";
    public string AvatarUrl { get; set; } = "";
    public DateTime LastSeen { get; set; } = DateTime.UtcNow;
    public bool IsOnline { get; set; } = false;
    
    // Navigation properties
    public virtual ICollection<ChatMessageModel> SentMessages { get; set; } = new List<ChatMessageModel>();
    public virtual ICollection<ChatMessageModel> ReceivedMessages { get; set; } = new List<ChatMessageModel>();
    public virtual ICollection<GroupMember> GroupMemberships { get; set; } = new List<GroupMember>();
}