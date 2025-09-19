using BlazorChatApp.Models.Chat;
using BlazorChatApp.Models.Identity;
using Microsoft.EntityFrameworkCore;

namespace BlazorChatApp.Services;

public class GroupService
{
    private readonly ApplicationDbContext _context;
    
    public GroupService(ApplicationDbContext context)
    {
        _context = context;
    }
    
    // Yeni grup oluştur
    public async Task<Group> CreateGroupAsync(string name, string? description, string avatarUrl, Guid createdById)
    {
        var group = new Group
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = description ?? "",
            AvatarUrl = avatarUrl,
            CreatedById = createdById,
            CreatedAt = DateTime.UtcNow
        };
        
        _context.Groups.Add(group);
        
        // Grup sahibini otomatik olarak admin üye yap
        var creatorMembership = new GroupMember
        {
            GroupId = group.Id,
            UserId = createdById,
            IsAdmin = true,
            JoinedAt = DateTime.UtcNow,
            LastReadAt = DateTime.UtcNow
        };
        
        _context.GroupMembers.Add(creatorMembership);
        await _context.SaveChangesAsync();
        
        return group;
    }
    
    // Gruba üyeler ekle (toplu)
    public async Task AddMembersToGroupAsync(Guid groupId, List<Guid> userIds)
    {
        var group = await _context.Groups.FindAsync(groupId);
        if (group == null) throw new ArgumentException("Grup bulunamadı");
        
        // Mevcut üyeleri kontrol et
        var existingMemberIds = await _context.GroupMembers
            .Where(gm => gm.GroupId == groupId)
            .Select(gm => gm.UserId)
            .ToListAsync();
        
        var newMemberIds = userIds.Where(id => !existingMemberIds.Contains(id)).ToList();
        
        if (!newMemberIds.Any()) return;
        
        var newMembers = newMemberIds.Select(userId => new GroupMember
        {
            GroupId = groupId,
            UserId = userId,
            IsAdmin = false,
            JoinedAt = DateTime.UtcNow,
            LastReadAt = DateTime.UtcNow
        }).ToList();
        
        _context.GroupMembers.AddRange(newMembers);
        await _context.SaveChangesAsync();
    }
    
    // Gruptan üye çıkar
    public async Task RemoveMemberFromGroupAsync(Guid groupId, Guid userId)
    {
        var membership = await _context.GroupMembers
            .FirstOrDefaultAsync(gm => gm.GroupId == groupId && gm.UserId == userId);
        
        if (membership == null) return;
        
        // Grup sahibi kendini çıkaramaz
        var group = await _context.Groups.FindAsync(groupId);
        if (group?.CreatedById == userId)
            throw new InvalidOperationException("Grup sahibi gruptan çıkamaz");
        
        _context.GroupMembers.Remove(membership);
        await _context.SaveChangesAsync();
    }
    
    // Grup üyelerini getir
    public async Task<List<GroupMember>> GetGroupMembersAsync(Guid groupId)
    {
        return await _context.GroupMembers
            .Where(gm => gm.GroupId == groupId)
            .Include(gm => gm.User)
            .OrderByDescending(gm => gm.IsAdmin)
            .ThenBy(gm => gm.User.DisplayName)
            .ToListAsync();
    }
    
    // Kullanıcının üyesi olduğu grupları getir
    public async Task<List<Group>> GetUserGroupsAsync(Guid userId)
    {
        return await _context.GroupMembers
            .Where(gm => gm.UserId == userId)
            .Include(gm => gm.Group)
            .Select(gm => gm.Group)
            .OrderBy(g => g.Name)
            .ToListAsync();
    }
    
    // Grup bilgilerini getir
    public async Task<Group?> GetGroupAsync(Guid groupId)
    {
        return await _context.Groups
            .Include(g => g.CreatedBy)
            .Include(g => g.Members)
                .ThenInclude(m => m.User)
            .FirstOrDefaultAsync(g => g.Id == groupId);
    }
    
    // Grup bilgilerini güncelle
    public async Task UpdateGroupAsync(Guid groupId, string name, string? description, string avatarUrl, Guid updatedById)
    {
        var group = await _context.Groups.FindAsync(groupId);
        if (group == null) throw new ArgumentException("Grup bulunamadı");
        
        // Sadece admin veya grup sahibi güncelleyebilir
        var membership = await _context.GroupMembers
            .FirstOrDefaultAsync(gm => gm.GroupId == groupId && gm.UserId == updatedById);
        
        if (membership == null || (!membership.IsAdmin && group.CreatedById != updatedById))
            throw new UnauthorizedAccessException("Grup bilgilerini güncelleme yetkisi yok");
        
        group.Name = name;
        group.Description = description ?? "";
        group.AvatarUrl = avatarUrl;
        
        await _context.SaveChangesAsync();
    }
    
    // Grubu sil
    public async Task DeleteGroupAsync(Guid groupId, Guid deletedById)
    {
        var group = await _context.Groups.FindAsync(groupId);
        if (group == null) throw new ArgumentException("Grup bulunamadı");
        
        // Sadece grup sahibi silebilir
        if (group.CreatedById != deletedById)
            throw new UnauthorizedAccessException("Grup silme yetkisi yok");
        
        // Grup üyelerini sil
        var members = await _context.GroupMembers
            .Where(gm => gm.GroupId == groupId)
            .ToListAsync();
        _context.GroupMembers.RemoveRange(members);
        
        // Grup mesajlarını sil (opsiyonel - soft delete yapılabilir)
        var messages = await _context.ChatMessages
            .Where(m => m.GroupId == groupId)
            .ToListAsync();
        _context.ChatMessages.RemoveRange(messages);
        
        // Grubu sil
        _context.Groups.Remove(group);
        await _context.SaveChangesAsync();
    }
    
    // Kullanıcının grupta yetki kontrolü
    public async Task<bool> IsUserAdminInGroupAsync(Guid groupId, Guid userId)
    {
        var membership = await _context.GroupMembers
            .FirstOrDefaultAsync(gm => gm.GroupId == groupId && gm.UserId == userId);
        
        return membership?.IsAdmin == true;
    }
    
    // Kullanıcı grupta üye mi kontrolü
    public async Task<bool> IsUserMemberOfGroupAsync(Guid groupId, Guid userId)
    {
        return await _context.GroupMembers
            .AnyAsync(gm => gm.GroupId == groupId && gm.UserId == userId);
    }
    
    // Grup istatistikleri
    public async Task<GroupStats> GetGroupStatsAsync(Guid groupId)
    {
        var memberCount = await _context.GroupMembers.CountAsync(gm => gm.GroupId == groupId);
        var messageCount = await _context.ChatMessages.CountAsync(m => m.GroupId == groupId);
        var lastMessage = await _context.ChatMessages
            .Where(m => m.GroupId == groupId)
            .OrderByDescending(m => m.SentAt)
            .FirstOrDefaultAsync();
        
        return new GroupStats
        {
            MemberCount = memberCount,
            MessageCount = messageCount,
            LastMessageAt = lastMessage?.SentAt
        };
    }
}

