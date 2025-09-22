using BlazorChatApp.Models.Identity;

namespace BlazorChatApp.Models.Chat;

public class ChatMessageModel
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SenderId { get; set; }
    public Guid? ReceiverId { get; set; } // null ise grup mesajı
    public Guid? GroupId { get; set; } // grup mesajı ise
    public string Content { get; set; } = "";
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
    public bool IsRead { get; set; } = false;
    public bool IsDeleted { get; set; } = false;
    // Mevcut alanların altına ekle
    public MessageType MessageType { get; set; } = MessageType.Text;
    public string? FileUrl { get; set; }
    public string? FileName { get; set; }
    public long? FileSize { get; set; }
    public string? MimeType { get; set; }
    public string? ThumbnailUrl { get; set; } // resim/video için küçük önizleme
    
    // Navigation properties
    public virtual AppUser Sender { get; set; } = null!;
    public virtual AppUser? Receiver { get; set; }
    public virtual Group? Group { get; set; }
}