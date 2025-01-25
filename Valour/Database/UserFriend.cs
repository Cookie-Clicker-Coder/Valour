﻿using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Valour.Shared.Models;

namespace Valour.Database;

/// <summary>
/// Friend objects represent a one-way declaration of friendship. To be actual friends, there must be a
/// UserFriend object going both ways. Like, you know, a real friendship.
/// 
/// ... I'll be your friend!
/// </summary>
[Table("user_friends")]
public class UserFriend : ISharedUserFriend
{
    ///////////////////////////
    // Relational Properties //
    ///////////////////////////
    
    /// <summary>
    /// The user who added the friend
    /// </summary>
    [ForeignKey("UserId")]
    public virtual User User { get; set; }

    /// <summary>
    /// The user being added as a friend
    /// (friendzoned)
    /// </summary>
    [ForeignKey("FriendId")]
    public virtual User Friend { get; set; }
    
    ///////////////////////
    // Entity Properties //
    ///////////////////////
    
    /// <summary>
    /// The id of the user friend model
    /// </summary>
    [Column("model_id")]
    public long Id { get; set; }

    /// <summary>
    /// The id of the user who added the friend
    /// </summary>
    [Column("user_id")]
    public long UserId { get; set; }

    /// <summary>
    /// The id of the user being added as a friend
    /// (friendzoned)
    /// </summary>
    [Column("friend_id")]
    public long FriendId { get; set; }

    public object GetId()
    {
        return (UserId, FriendId);
    }

    public static void SetUpDbModel(ModelBuilder builder)
    {
        builder.Entity<UserFriend>(e =>
        {
            // ToTable
            e.ToTable("user_friends");
            
            // Key
            e.HasKey(x => x.GetId());
            
            // Properties
            e.Property(x => x.UserId)
                .HasColumnName("user_id");
            
            e.Property(x => x.FriendId)
                .HasColumnName("friend_id");
            
            e.Property(x => x.Id)
                .HasColumnName("id");
            
        });
    }
}
