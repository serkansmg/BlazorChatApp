// Services/FriendService.cs

using BlazorChatApp.Models.Chat;
using BlazorChatApp.Models.Identity;
using Microsoft.EntityFrameworkCore;

public class FriendService
{
    private readonly ApplicationDbContext _context;
    
    public FriendService(ApplicationDbContext context)
    {
        _context = context;
    }
    
    // Email ile kullanıcı ara
    public async Task<AppUser?> FindUserByEmailAsync(string email)
    {
        return await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
    }
    
    // Arkadaşlık durumunu kontrol et
    public async Task<FriendshipStatus> GetFriendshipStatusAsync(Guid userId1, Guid userId2)
    {
        // Zaten arkadaş mı?
        var friendship = await _context.Friendships
            .FirstOrDefaultAsync(f => (f.User1Id == userId1 && f.User2Id == userId2) ||
                                     (f.User1Id == userId2 && f.User2Id == userId1));
        if (friendship != null) return FriendshipStatus.Friends;
        
        // Bekleyen istek var mı?
        var pendingRequest = await _context.FriendRequests
            .FirstOrDefaultAsync(fr => ((fr.SenderId == userId1 && fr.ReceiverId == userId2) ||
                                       (fr.SenderId == userId2 && fr.ReceiverId == userId1)) &&
                                      fr.Status == FriendRequestStatus.Pending);
        if (pendingRequest != null)
        {
            return pendingRequest.SenderId == userId1 ? FriendshipStatus.RequestSent : FriendshipStatus.RequestReceived;
        }
        
        return FriendshipStatus.NotFriends;
    }
    
    // Arkadaşlık isteği gönder (mesajla birlikte)
    public async Task<FriendRequest> SendFriendRequestAsync(Guid senderId, Guid receiverId, string? message = null)
    {
        Console.WriteLine($"SendFriendRequestAsync - SenderId: {senderId}, ReceiverId: {receiverId}");
    
        var friendRequest = new FriendRequest
        {
            SenderId = senderId,
            ReceiverId = receiverId,
            Message = message,
            Status = FriendRequestStatus.Pending
        };
    
        _context.FriendRequests.Add(friendRequest);
        await _context.SaveChangesAsync();
    
        Console.WriteLine($"Friend request saved to database with ID: {friendRequest.Id}");
    
        // Kayıt sonrası doğrulama
        var savedRequest = await _context.FriendRequests.FindAsync(friendRequest.Id);
        Console.WriteLine($"Verification - saved request exists: {savedRequest != null}");
    
        return friendRequest;
    }
    
    // Arkadaşlık isteğini kabul et
    public async Task AcceptFriendRequestAsync(Guid requestId)
    {
        var request = await _context.FriendRequests.FindAsync(requestId);
        if (request == null || request.Status != FriendRequestStatus.Pending) return;
        
        // İsteği kabul edildi olarak işaretle
        request.Status = FriendRequestStatus.Accepted;
        request.RespondedAt = DateTime.UtcNow;
        
        // Arkadaşlık oluştur
        var friendship = new Friendship
        {
            User1Id = request.SenderId,
            User2Id = request.ReceiverId
        };
        
        _context.Friendships.Add(friendship);
        await _context.SaveChangesAsync();
    }
    // Kullanıcıya gelen bekleyen istekleri getir
    public async Task<List<FriendRequest>> GetPendingRequestsAsync(Guid userId)
    {
        Console.WriteLine($"GetPendingRequestsAsync called for userId: {userId}");
    
        var requests = await _context.FriendRequests
            .Include(fr => fr.Sender)
            .Where(fr => fr.ReceiverId == userId && fr.Status == FriendRequestStatus.Pending)
            .OrderByDescending(fr => fr.RequestedAt)
            .ToListAsync();
        
        Console.WriteLine($"Raw query found {requests.Count} requests");
    
        foreach (var req in requests)
        {
            Console.WriteLine($"Request ID: {req.Id}, Sender: {req.SenderId}, Receiver: {req.ReceiverId}, Status: {req.Status}");
        }
    
        return requests;
    }

// Arkadaşlık isteğini reddet
    public async Task RejectFriendRequestAsync(Guid requestId)
    {
        var request = await _context.FriendRequests.FindAsync(requestId);
        if (request == null || request.Status != FriendRequestStatus.Pending) return;
    
        request.Status = FriendRequestStatus.Rejected;
        request.RespondedAt = DateTime.UtcNow;
    
        await _context.SaveChangesAsync();
    }
    
    // Kullanıcının arkadaş listesini getir
    public async Task<List<AppUser>> GetUserFriendsAsync(Guid userId)
    {
        var friendships = await _context.Friendships
            .Where(f => f.User1Id == userId || f.User2Id == userId)
            .Include(f => f.User1)
            .Include(f => f.User2)
            .ToListAsync();
    
        var friends = new List<AppUser>();
    
        foreach (var friendship in friendships)
        {
            // Karşı tarafı (arkadaşı) al
            var friend = friendship.User1Id == userId ? friendship.User2 : friendship.User1;
            friends.Add(friend);
        }
    
        return friends.OrderBy(f => f.DisplayName).ToList();
    }

// Kullanıcının arkadaş sayısını getir
    public async Task<int> GetUserFriendCountAsync(Guid userId)
    {
        return await _context.Friendships
            .CountAsync(f => f.User1Id == userId || f.User2Id == userId);
    }

// İki kullanıcının arkadaş olup olmadığını kontrol et
    public async Task<bool> AreFriendsAsync(Guid userId1, Guid userId2)
    {
        return await _context.Friendships
            .AnyAsync(f => (f.User1Id == userId1 && f.User2Id == userId2) ||
                           (f.User1Id == userId2 && f.User2Id == userId1));
    }
}

