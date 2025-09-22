using BlazorChatApp.Models.Identity;

namespace BlazorChatApp.Models.Chat;

public class Friendship
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid User1Id { get; set; }
    public Guid User2Id { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public virtual AppUser User1 { get; set; } = null!;
    public virtual AppUser User2 { get; set; } = null!;
}