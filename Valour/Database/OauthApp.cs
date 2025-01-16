using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Valour.Shared.Models;

namespace Valour.Database;

[Table("oauth_apps")]
public class OauthApp : ISharedOauthApp
{
    ///////////////////////////
    // Relational Properties //
    ///////////////////////////
    
    [ForeignKey("OwnerId")]
    public virtual User Owner { get; set; }
    
    
    
    ///////////////////////
    // Entity Properties //
    ///////////////////////
    
    [Key]
    [Column("id")]
    public long Id { get; set; }

    /// <summary>
    /// The secret key for the app
    /// </summary>
    [Column("secret")]
    public string Secret { get; set; }

    
    /// <summary>
    /// The User for Oauthapps
    /// </summary>
    

    /// <summary>
    /// The ID of the user that created this app
    /// </summary>
    [Column("owner_id")]
    public long OwnerId { get; set; }

    /// <summary>
    /// The amount of times this app has been used
    /// </summary>
    [Column("uses")]
    public int Uses { get; set; }

    /// <summary>
    /// The image used to represent the app
    /// </summary>
    [Column("image_url")]
    public string ImageUrl { get; set; }

    /// <summary>
    /// The name of the app
    /// </summary>
    [Column("name")]
    public string Name { get; set; }

    /// <summary>
    /// The redirect url for authorization
    /// </summary>
    [Column("redirect_url")]
    public string RedirectUrl { get; set; }

    public static void SetupDDModel(ModelBuilder builder)
    {
        builder.Entity<OauthApp>(e =>
        {
            // Table
            e.ToTable("oauth_apps");
            
            // Keys
            e.HasKey(x => x.Id);
            
            // Properties
            e.Property(x => x.Id)
                .HasColumnName("id");
            
            e.Property(x => x.Secret)
                .HasColumnName("secret");
            
            e.Property(x => x.OwnerId)
                .HasColumnName("owner_id");
            
            e.Property(x => x.Uses)
                .HasColumnName("uses");
            
            e.Property(x => x.ImageUrl)
                .HasColumnName("image_url");
            
            e.Property(x => x.Name)
                .HasColumnName("name");
            
            // Relationships

            e.HasOne(x => x.Owner)
                .WithMany(x => x.OwnedApps)
                .HasForeignKey(x => x.OwnerId);

            // Indices

            e.HasIndex(x => new { x.OwnerId, x.Uses })
                .IsUnique();

        });
    }
}