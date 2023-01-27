using IdGen;
using StackExchange.Redis;
using Valour.Database.Context;
using Valour.Server.Database;
using Valour.Shared.Authorization;
using Valour.Shared.Models;

namespace Valour.Server.Services;

public class PlanetRoleService
{
    private readonly ValourDB _db;
    private readonly ILogger<PlanetChatChannelService> _logger;
    private readonly CoreHubService _coreHub;

    public PlanetRoleService(
        ValourDB db, 
        ILogger<PlanetChatChannelService> logger, 
        CoreHubService coreHub)
    {
        _db = db;
        _logger = logger;
        _coreHub = coreHub;
    }

    /// <summary>
    /// Returns the planert role with the given id
    /// </summary>
    public async ValueTask<PlanetRole> GetAsync(long id) =>
        (await _db.PlanetRoles.FindAsync(id)).ToModel();

    public async Task<IResult> CreateAsync(PlanetRole role)
    {
        role.Position = await _db.PlanetRoles.CountAsync(x => x.PlanetId == role.PlanetId);
        role.Id = IdManager.Generate();

        try
        {
            await _db.AddAsync(role);
            await _db.SaveChangesAsync();
        }
        catch (System.Exception e)
        {
            _logger.LogError(e.Message);
            return Results.Problem(e.Message);
        }

        _coreHub.NotifyPlanetItemChange(role);

        return Results.Created($"api/planetroles/{role.Id}", role);
    }

    public async Task<IResult> PutAsync(PlanetRole oldRole, PlanetRole updatedRole)
    {
        if (updatedRole.PlanetId != oldRole.PlanetId)
            return Results.BadRequest("You cannot change what planet.");

        if (updatedRole.Position != oldRole.Position)
            return Results.BadRequest("Position cannot be changed directly.");
        try
        {
            _db.Entry(oldRole).State = EntityState.Detached;
            _db.PlanetRoles.Update(updatedRole.ToDatabase());
            await _db.SaveChangesAsync();
        }
        catch (System.Exception e)
        {
            _logger.LogError(e.Message);
            return Results.Problem(e.Message);
        }

        _coreHub.NotifyPlanetItemChange(updatedRole);

        return Results.Json(updatedRole);
    }

    public async Task<List<PermissionsNode>> GetNodesAsync(PlanetRole role) =>
        await _db.PermissionsNodes.Where(x => x.RoleId == role.Id).Select(x => x.ToModel()).ToListAsync();

    public async Task<List<PermissionsNode>> GetChannelNodesAsync(PlanetRole role) =>
        await _db.PermissionsNodes.Where(x => x.TargetType == PermissionsTargetType.PlanetChatChannel &&
                                          x.RoleId == role.Id).ToListAsync();

    public async Task<List<PermissionsNode>> GetCategoryNodesAsync(PlanetRole role) =>
        await _db.PermissionsNodes.Where(x => x.TargetType == PermissionsTargetType.PlanetCategoryChannel &&
                                          x.RoleId == role.Id).ToListAsync();

    public async Task<PermissionsNode> GetChatChannelNodeAsync(PlanetChatChannel channel, PlanetRole role) =>
        await _db.PermissionsNodes.FirstOrDefaultAsync(x => x.TargetId == channel.Id &&
                                                           x.TargetType == PermissionsTargetType.PlanetChatChannel &&
                                                           x.RoleId == role.Id);

    public async Task<PermissionsNode> GetChatChannelNodeAsync(PlanetCategory category, PlanetRole role) =>
        await _db.PermissionsNodes.FirstOrDefaultAsync(x => x.TargetId == category.Id &&
                                                           x.TargetType == PermissionsTargetType.PlanetChatChannel &&
                                                           x.RoleId == role.Id);

    public async Task<PermissionsNode> GetCategoryNodeAsync(PlanetCategory category, PlanetRole role) =>
        await _db.PermissionsNodes.FirstOrDefaultAsync(x => x.TargetId == category.Id &&
                                                           x.TargetType == PermissionsTargetType.PlanetCategoryChannel &&
                                                           x.RoleId == role.Id);

    public async Task<PermissionState> GetPermissionStateAsync(Permission permission, PlanetChatChannel channel, PlanetRole role) =>
        await GetPermissionStateAsync(permission, channel, role);

    public async Task<PermissionState> GetPermissionStateAsync(Permission permission, long channelId, PlanetRole role) =>
        (await _db.PermissionsNodes.FirstOrDefaultAsync(x => x.RoleId == role.Id && x.TargetId == channelId)).GetPermissionState(permission);

    public async Task DeleteAsync(PlanetRole role)
    {
        // Remove all members
        var members = _db.PlanetRoleMembers.Where(x => x.RoleId == role.Id);
        _db.PlanetRoleMembers.RemoveRange(members);

        // Remove role nodes
        var nodes = await GetNodesAsync(role);

        _db.PermissionsNodes.RemoveRange(nodes.Select(x => x.ToDatabase());

        // Remove the role
        _db.PlanetRoles.Remove(role.ToDatabase());
    }
}