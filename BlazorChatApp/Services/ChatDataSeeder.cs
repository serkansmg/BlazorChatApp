using BlazorChatApp.Models.Chat;
using BlazorChatApp.Models.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BlazorChatApp.Services;

public class ChatDataSeeder
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<AppUser> _userManager;
    
    public ChatDataSeeder(ApplicationDbContext context, UserManager<AppUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }
    
    public async Task SeedChatDataAsync(Guid currentUserId)
    {
        // Eğer zaten data varsa seed etme
        if (await _context.ChatMessages.AnyAsync() || await _context.Groups.AnyAsync())
            return;
            
        // Dummy kullanıcılar oluştur
        var dummyUsers = new List<AppUser>();
        
        // Ahmet Yılmaz
        var ahmet = new AppUser
        {
            Name = "Ahmet",
            Surname = "Yılmaz",
            UserName = "ahmet.yilmaz",
            Email = "ahmet@example.com",
            EmailConfirmed = true,
            AvatarUrl = "pics/man.png",
            IsOnline = true,
            LastSeen = DateTime.UtcNow.AddMinutes(-5)
        };
        
        var ahmetResult = await _userManager.CreateAsync(ahmet, "Pass123!!");
        if (ahmetResult.Succeeded)
        {
            dummyUsers.Add(ahmet);
        }
        
        // Ayşe Kaya
        var ayse = new AppUser
        {
            Name = "Ayşe",
            Surname = "Kaya",
            UserName = "ayse.kaya",
            Email = "ayse@example.com",
            EmailConfirmed = true,
            AvatarUrl = "pics/woman.png",
            IsOnline = false,
            LastSeen = DateTime.UtcNow.AddHours(-2)
        };
        
        var ayseResult = await _userManager.CreateAsync(ayse, "Pass123!!");
        if (ayseResult.Succeeded)
        {
            dummyUsers.Add(ayse);
        }
        
        if (!dummyUsers.Any()) return; // Kullanıcı oluşturulamazsa devam etme
        
        // Dummy grup oluştur
        var projeEkibi = new Group
        {
            Id = Guid.NewGuid(),
            Name = "Proje Ekibi",
            Description = "Ana proje geliştirme ekibi",
            AvatarUrl = "pics/group.png",
            CreatedById = currentUserId,
            CreatedAt = DateTime.UtcNow.AddDays(-7)
        };
        
        _context.Groups.Add(projeEkibi);
        await _context.SaveChangesAsync();
        
        // Grup üyelikleri
        var groupMembers = new List<GroupMember>
        {
            new GroupMember { GroupId = projeEkibi.Id, UserId = currentUserId, IsAdmin = true }
        };
        
        // Oluşturulan kullanıcıları gruba ekle
        foreach (var user in dummyUsers)
        {
            groupMembers.Add(new GroupMember 
            { 
                GroupId = projeEkibi.Id, 
                UserId = user.Id, 
                IsAdmin = false 
            });
        }
        
        _context.GroupMembers.AddRange(groupMembers);
        await _context.SaveChangesAsync();
        
        //PrepareDummyMessages(currentUserId, dummyUsers, projeEkibi);
        await _context.SaveChangesAsync();
    }

    private void PrepareDummyMessages(Guid currentUserId, List<AppUser> dummyUsers, Group projeEkibi)
    {
        // Dummy mesajlar
        var ahmetId = dummyUsers[0].Id;
        var ayseId = dummyUsers.Count > 1 ? dummyUsers[1].Id : ahmetId;
        
        var dummyMessages = new List<ChatMessageModel>
        {
            // Ahmet ile mesajlaşma
            new ChatMessageModel
            {
                SenderId = ahmetId,
                ReceiverId = currentUserId,
                Content = "Merhaba! Nasılsın?",
                SentAt = DateTime.Now.AddMinutes(-15),
                IsRead = false
            },
            new ChatMessageModel
            {
                SenderId = currentUserId,
                ReceiverId = ahmetId,
                Content = "İyiyim, teşekkürler. Sen nasılsın?",
                SentAt = DateTime.Now.AddMinutes(-12),
                IsRead = true
            },
            new ChatMessageModel
            {
                SenderId = ahmetId,
                ReceiverId = currentUserId,
                Content = "Ben de iyiyim. Proje nasıl gidiyor?",
                SentAt = DateTime.Now.AddMinutes(-8),
                IsRead = false
            },
            
            // Grup mesajları
            new ChatMessageModel
            {
                SenderId = currentUserId,
                GroupId = projeEkibi.Id,
                Content = "Herkese merhaba! Yeni proje planını paylaştım.",
                SentAt = DateTime.Now.AddMinutes(-30),
                IsRead = true
            },
            new ChatMessageModel
            {
                SenderId = ahmetId,
                GroupId = projeEkibi.Id,
                Content = "Toplantı için hazır mıyız?",
                SentAt = DateTime.Now.AddMinutes(-10),
                IsRead = false
            }
        };
        
        // Ayşe varsa onunla da mesaj ekle
        if (dummyUsers.Count > 1)
        {
            dummyMessages.AddRange(new[]
            {
                new ChatMessageModel
                {
                    SenderId = ayseId,
                    ReceiverId = currentUserId,
                    Content = "Toplantı raporunu gördün mü?",
                    SentAt = DateTime.UtcNow.AddHours(-3),
                    IsRead = true
                },
                new ChatMessageModel
                {
                    SenderId = currentUserId,
                    ReceiverId = ayseId,
                    Content = "Evet, çok güzel olmuş. Teşekkürler!",
                    SentAt = DateTime.UtcNow.AddHours(-2),
                    IsRead = true
                }
            });
        }
        
        _context.ChatMessages.AddRange(dummyMessages);
    }
}