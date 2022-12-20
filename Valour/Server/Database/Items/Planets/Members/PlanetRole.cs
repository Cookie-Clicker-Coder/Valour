﻿using Microsoft.AspNetCore.Mvc;
using System.Drawing;
using System.Security.Cryptography;
using Valour.Server.Database.Items.Authorization;
using Valour.Server.Database.Items.Channels.Planets;
using Valour.Server.EndpointFilters;
using Valour.Server.EndpointFilters.Attributes;
using Valour.Server.Hubs;
using Valour.Server.Services;
using Valour.Shared.Authorization;
using Valour.Shared.Items.Authorization;
using Valour.Shared.Items.Planets.Members;

/*  Valour - A free and secure chat client
 *  Copyright (C) 2021 Vooper Media LLC
 *  This program is subject to the GNU Affero General Public license
 *  A copy of the license should be included - if not, see <http://www.gnu.org/licenses/>
 */


namespace Valour.Server.Database.Items.Planets.Members;

[Table("planet_roles")]
public class PlanetRole : Item, IPlanetItem, ISharedPlanetRole
{
    #region IPlanetItem Implementation

    [JsonIgnore]
    [ForeignKey("PlanetId")]
    public Planet Planet { get; set; }

    [Column("planet_id")]
    public long PlanetId { get; set; }

    public ValueTask<Planet> GetPlanetAsync(ValourDB db) =>
        IPlanetItem.GetPlanetAsync(this, db);

    [JsonIgnore]
    public override string BaseRoute =>
        $"api/planet/{{planetId}}/{nameof(PlanetRole)}";

    #endregion

    [InverseProperty("Role")]
    [JsonIgnore]
    public virtual ICollection<PermissionsNode> PermissionNodes { get; set; }

    /// <summary>
    /// The position of the role: Lower has more authority
    /// </summary>
    [Column("position")]
    public int Position { get; set; }

    /// <summary>
    /// The planet permissions for the role
    /// </summary>
    [Column("permissions")]
    public long Permissions { get; set; }

    // RGB Components for role color
    [Column("red")]
    public byte Red { get; set; }

    [Column("green")]
    public byte Green { get; set; }

    [Column("blue")]
    public byte Blue { get; set; }

    // Formatting options
    [Column("bold")]
    public bool Bold { get; set; }

    [Column("italics")]
    public bool Italics { get; set; }

    [Column("name")]
    public string Name { get; set; }

    public int GetAuthority() =>
        ISharedPlanetRole.GetAuthority(this);

    public Color GetColor() =>
        ISharedPlanetRole.GetColor(this);

    public string GetColorHex() =>
        ISharedPlanetRole.GetColorHex(this);

    public bool HasPermission(PlanetPermission perm) =>
        ISharedPlanetRole.HasPermission(this, perm);

    public ICollection<PermissionsNode> GetNodes(ValourDB db)
    {
        PermissionNodes ??= db.PermissionsNodes.Where(x => x.RoleId == Id).ToList();
        return PermissionNodes;
    }

    public ICollection<PermissionsNode> GetChannelNodes(ValourDB db)
    {
        PermissionNodes ??= db.PermissionsNodes.Where(x => x.RoleId == Id).ToList();
        return PermissionNodes.Where(x => x.TargetType == PermissionsTargetType.PlanetChatChannel &&
                                          x.RoleId == Id).ToList();
    }

    public ICollection<PermissionsNode> GetCategoryNodes(ValourDB db)
    {
        PermissionNodes ??= db.PermissionsNodes.Where(x => x.RoleId == Id).ToList();
        return PermissionNodes.Where(x => x.TargetType == PermissionsTargetType.PlanetCategoryChannel &&
                                          x.RoleId == Id).ToList();
    }

    public async Task<PermissionsNode> GetChatChannelNodeAsync(PlanetChatChannel channel, ValourDB db) =>
        await db.PermissionsNodes.FirstOrDefaultAsync(x => x.TargetId == channel.Id &&
                                                           x.TargetType == PermissionsTargetType.PlanetChatChannel &&
                                                           x.RoleId == Id);

    public async Task<PermissionsNode> GetChatChannelNodeAsync(PlanetCategoryChannel category, ValourDB db) =>
        await db.PermissionsNodes.FirstOrDefaultAsync(x => x.TargetId == category.Id &&
                                                           x.TargetType == PermissionsTargetType.PlanetChatChannel &&
                                                           x.RoleId == Id);

    public async Task<PermissionsNode> GetCategoryNodeAsync(PlanetCategoryChannel category, ValourDB db) =>
        await db.PermissionsNodes.FirstOrDefaultAsync(x => x.TargetId == category.Id &&
                                                           x.TargetType == PermissionsTargetType.PlanetCategoryChannel &&
                                                           x.RoleId == Id);

    public async Task<PermissionState> GetPermissionStateAsync(Permission permission, PlanetChatChannel channel, ValourDB db) =>
        await GetPermissionStateAsync(permission, channel.Id, db);

    public async Task<PermissionState> GetPermissionStateAsync(Permission permission, long channelId, ValourDB db) =>
        (await db.PermissionsNodes.FirstOrDefaultAsync(x => x.RoleId == Id && x.TargetId == channelId)).GetPermissionState(permission);

    public async Task DeleteAsync(ValourDB db)
    {
        // Remove all members
        var members = db.PlanetRoleMembers.Where(x => x.RoleId == Id);
        db.PlanetRoleMembers.RemoveRange(members);

        // Remove role nodes
        var nodes = GetNodes(db);

        db.PermissionsNodes.RemoveRange(nodes);

        // Remove self
        db.PlanetRoles.Remove(this);
    }

    [ValourRoute(HttpVerbs.Get), TokenRequired]
    [UserPermissionsRequired(UserPermissionsEnum.Membership)]
    [PlanetMembershipRequired]
    public static async Task<IResult> GetRouteAsync(
        long id, 
        ValourDB db)
    {
        var role = await FindAsync<PlanetRole>(id, db);

        if (role is null)
            return ValourResult.NotFound<PlanetRole>();

        return Results.Json(role);
    }

    [ValourRoute(HttpVerbs.Post), TokenRequired]
    [UserPermissionsRequired(UserPermissionsEnum.PlanetManagement)]
    [PlanetMembershipRequired(permissions: PlanetPermissionsEnum.ManageRoles)]
    public static async Task<IResult> PostRouteAsync(
        [FromBody] PlanetRole role, 
        HttpContext ctx,
        ValourDB db,
        CoreHubService hubService,
        ILogger<PlanetRole> logger)
    {
        var authMember = ctx.GetMember();

        role.Position = await db.PlanetRoles.CountAsync(x => x.PlanetId == role.PlanetId);
        role.Id = IdManager.Generate();

        if (role.GetAuthority() > await authMember.GetAuthorityAsync(db))
            return ValourResult.Forbid("You cannot create roles with higher authority than your own.");

        try
        {
            await db.AddAsync(role);
            await db.SaveChangesAsync();
        }
        catch (System.Exception e)
        {
            logger.LogError(e.Message);
            return Results.Problem(e.Message);
        }

        hubService.NotifyPlanetItemChange(role);

        return Results.Created(role.GetUri(), role);

    }

    [ValourRoute(HttpVerbs.Put), TokenRequired]
    [UserPermissionsRequired(UserPermissionsEnum.PlanetManagement)]
    [PlanetMembershipRequired(permissions: PlanetPermissionsEnum.ManageRoles)]
    public static async Task<IResult> PutRouteAsync(
        [FromBody] PlanetRole role, 
        long id,
        ValourDB db,
        CoreHubService hubService,
        ILogger<PlanetRole> logger)
    {
        var oldRole = await FindAsync<PlanetRole>(id, db);

        if (role.PlanetId != oldRole.PlanetId)
            return Results.BadRequest("You cannot change what planet.");

        if (role.Position != oldRole.Position)
            return Results.BadRequest("Position cannot be changed directly.");
        try
        {
            db.Entry(oldRole).State = EntityState.Detached;
            db.PlanetRoles.Update(role);
            await db.SaveChangesAsync();
        }
        catch (System.Exception e)
        {
            logger.LogError(e.Message);
            return Results.Problem(e.Message);
        }

        hubService.NotifyPlanetItemChange(role);

        return Results.Json(role);

    }

    [ValourRoute(HttpVerbs.Delete), TokenRequired]
    [UserPermissionsRequired(UserPermissionsEnum.PlanetManagement)]
    [PlanetMembershipRequired(permissions: PlanetPermissionsEnum.ManageRoles)]
    public static async Task<IResult> DeleteRouteAsync(
        long id,
        ValourDB db,
        CoreHubService hubService,
        ILogger<PlanetRole> logger)
    {
        var role = await FindAsync<PlanetRole>(id, db);

        try
        {
            await role.DeleteAsync(db);
            await db.SaveChangesAsync();
        }
        catch (System.Exception e)
        {
            logger.LogError(e.Message);
            return Results.Problem(e.Message);
        }

        hubService.NotifyPlanetItemDelete(role);

        return Results.NoContent();

    }
}