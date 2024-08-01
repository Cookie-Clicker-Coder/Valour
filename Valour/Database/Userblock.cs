using Microsoft.EntityFrameworkCore
namespace Valour.Database;

public class BlockedUsers
{
    public virtual User SourceUser { get; set; }
    public virtual User TargetUser { get; set; }

    /// <summary>
    ///  The id of the user who intiated the block
    /// </summary>
    public long SourceUserId { get; set; }

    /// <summar>
    /// the user who is being blocked
    /// </summary>
    public long TargetUserId { get; set; }
    
    public string Reason { get; set; }
    
    public DateTime Timestamp { get; set; }

    public static void OnModelCreating(ModelBuilder builder)
    {
        builder.Entity<BlockedUsers>(e =>
        {
            e.ToTable("blocked_users");

            e.Property(x => x.SourceUser)
                .HasColumnName("source_user_id");
            e.Property(x => x.TargetUser)
                .HasColumnName("target_user_id");
            e.Property(x => x.Reason)
                .HasColumnName("reason");
                .HasMaxLength(64);
            e.Property(x => x.Timestamp)
                .HasColumnName("timestamp");
            e.HasKey(x => new { x.SourceUserId, x.TargetUserId });

            e.HasOne(x => x.SourceUser)
                .WithMany(x => x.BlockedUsers);
            
            e.HasOne(x => x.TargetUser)
                .WithMany(x => x.BlockedBy);

        });

    }
}