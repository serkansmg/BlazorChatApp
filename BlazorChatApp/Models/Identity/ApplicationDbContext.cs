using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using BlazorChatApp.Models.Chat;

namespace BlazorChatApp.Models.Identity;

public class ApplicationDbContext : IdentityDbContext<AppUser, AppRole, Guid>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    // Chat DbSets
    public DbSet<ChatMessageModel> ChatMessages { get; set; }
    public DbSet<Group> Groups { get; set; }
    public DbSet<GroupMember> GroupMembers { get; set; }
    public DbSet<FriendRequest> FriendRequests { get; set; }
    public DbSet<Friendship> Friendships { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // ChatMessageModel configurations
        builder.Entity<ChatMessageModel>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Content).IsRequired().HasMaxLength(4000);
            entity.Property(e => e.MessageType)
                .HasConversion<int>();
            
            // Sender relationship
            entity.HasOne(e => e.Sender)
                .WithMany(u => u.SentMessages)
                .HasForeignKey(e => e.SenderId)
                .OnDelete(DeleteBehavior.Restrict);
                
            // Receiver relationship (optional)
            entity.HasOne(e => e.Receiver)
                .WithMany(u => u.ReceivedMessages)
                .HasForeignKey(e => e.ReceiverId)
                .OnDelete(DeleteBehavior.Restrict);
                
            // Group relationship (optional)
            entity.HasOne(e => e.Group)
                .WithMany(g => g.Messages)
                .HasForeignKey(e => e.GroupId)
                .OnDelete(DeleteBehavior.Restrict);

            // Index for performance
            entity.HasIndex(e => new { e.SenderId, e.ReceiverId });
            entity.HasIndex(e => e.GroupId);
            entity.HasIndex(e => e.SentAt);
            
        });

        // Group configurations
        builder.Entity<Group>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Description).HasMaxLength(500);
            
            entity.HasOne(e => e.CreatedBy)
                .WithMany()
                .HasForeignKey(e => e.CreatedById)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // GroupMember configurations
        builder.Entity<GroupMember>(entity =>
        {
            entity.HasKey(e => e.Id);
            
            entity.HasOne(e => e.Group)
                .WithMany(g => g.Members)
                .HasForeignKey(e => e.GroupId)
                .OnDelete(DeleteBehavior.Cascade);
                
            entity.HasOne(e => e.User)
                .WithMany(u => u.GroupMemberships)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Unique constraint - user can join a group only once
            entity.HasIndex(e => new { e.GroupId, e.UserId }).IsUnique();
        });

        // AppUser configurations
        builder.Entity<AppUser>(entity =>
        {
            entity.Property(e => e.AvatarUrl).HasMaxLength(500);
        });
        
        // FriendRequest configurations
        builder.Entity<FriendRequest>(entity =>
        {
            entity.HasKey(e => e.Id);
    
            // Enum'u int olarak store et
            entity.Property(e => e.Status)
                .HasConversion<int>();
    
            entity.HasOne(e => e.Sender)
                .WithMany()
                .HasForeignKey(e => e.SenderId)
                .OnDelete(DeleteBehavior.Restrict);
        
            entity.HasOne(e => e.Receiver)
                .WithMany()
                .HasForeignKey(e => e.ReceiverId)
                .OnDelete(DeleteBehavior.Restrict);
        
            entity.HasIndex(e => new { e.SenderId, e.ReceiverId }).IsUnique();
        });

// Friendship configurations
        builder.Entity<Friendship>(entity =>
        {
            entity.HasKey(e => e.Id);
    
            entity.HasOne(e => e.User1)
                .WithMany()
                .HasForeignKey(e => e.User1Id)
                .OnDelete(DeleteBehavior.Restrict);
        
            entity.HasOne(e => e.User2)
                .WithMany()
                .HasForeignKey(e => e.User2Id)
                .OnDelete(DeleteBehavior.Restrict);
        
            entity.HasIndex(e => new { e.User1Id, e.User2Id }).IsUnique();
        });
    }
}