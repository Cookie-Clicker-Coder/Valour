﻿using Valour.Server.Database.Items.Channels;
using Valour.Server.Database.Items.Planets;
using Valour.Server.Database.Items.Planets.Members;
using Valour.Server.Services;
using Valour.Shared.Authorization;
using Valour.Shared.Items;
using Valour.Shared.Items.Authorization;
using Valour.Shared.Items.Channels.Planets;

namespace Valour.Server.Database.Items.Channels.Planets;

[Table("planet_channels")]
[JsonDerivedType(typeof(PlanetChatChannel), typeDiscriminator: nameof(PlanetChatChannel))]
[JsonDerivedType(typeof(PlanetVoiceChannel), typeDiscriminator: nameof(PlanetVoiceChannel))]
[JsonDerivedType(typeof(PlanetCategoryChannel), typeDiscriminator: nameof(PlanetCategoryChannel))]
public abstract class PlanetChannel : Channel, IPlanetItem, ISharedPlanetChannel, ISharedPermissionsTarget
{
    #region IPlanetItem Implementation

    [JsonIgnore]
    [ForeignKey("PlanetId")]
    public Planet Planet { get; set; }

    [Column("planet_id")]
    public long PlanetId { get; set; }

    public async ValueTask<Planet> GetPlanetAsync(PlanetService service)
    {
        Planet ??= await service.GetAsync(PlanetId);
        return Planet;
    }

    [JsonIgnore]
    public override string BaseRoute =>
        $"api/planet/{{planetId}}/{nameof(PlanetChannel)}";

    #endregion

    [JsonIgnore]
    [ForeignKey("ParentId")]
    public PlanetCategoryChannel Parent { get; set; }

    [Column("name")]
    public string Name { get; set; }

    [Column("position")]
    public int Position { get; set; }

    [Column("description")]
    public string Description { get; set; }

    [Column("parent_id")]
    public long? ParentId { get; set; }

    [Column("inherits_perms")]
    public bool InheritsPerms { get; set; }

    public abstract PermissionsTargetType PermissionsTargetType { get; }

    /// <summary>
    /// Returns the parent category of this channel
    /// </summary>
    public async Task<PlanetCategoryChannel> GetParentAsync(PlanetCategoryService service)
    {
        if (ParentId is null)
            return null;
        
        Parent ??= await service.GetAsync(ParentId.Value);
        return Parent;
    }

    public static async Task<bool> HasUniquePosition(ValourDB db, PlanetChannel channel) =>
        // Ensure position is not already taken
        !await db.PlanetChannels.AnyAsync(x => x.ParentId == channel.ParentId && // Same parent
                                                x.Position == channel.Position && // Same position
                                                x.Id != channel.Id); // Not self
}

