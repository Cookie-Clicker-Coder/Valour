using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Valour.Server.Requests;
using Valour.Shared.Authorization;
using Valour.Shared.Channels;
using Valour.Shared.Models;

namespace Valour.Server.Api.Dynamic;

public class ChannelApi
{
    [ValourRoute(HttpVerbs.Get, "api/planets/{planetId}/channels/{channelId}")]
    [UserRequired]
    public static async Task<IResult> GetPlanetChannelRouteAsync(
        long planetId,
        long channelId,
        ChannelService channelService,
        TokenService tokenService)
    {
        var token = await tokenService.GetCurrentTokenAsync();
        
        var channel = await channelService.GetPlanetChannelAsync(planetId, channelId);
        if (channel is null)
            return ValourResult.NotFound<Channel>();

        if (!await channelService.HasAccessAsync(channel, token.UserId))
            return ValourResult.Forbid("You are not a member of this channel");
        
        if (channel.ChannelType == ChannelTypeEnum.DirectChat)
        {
            if (!token.HasScope(UserPermissions.DirectMessages))
            {
                return ValourResult.Forbid("Token lacks permission to post messages in direct chat channels");
            }
        }
        
        return Results.Json(channel);
    }

    [ValourRoute(HttpVerbs.Put, "api/planets/{planetId}/channels/{channelId}")]
    [UserRequired(UserPermissionsEnum.PlanetManagement)]
    public static async Task<IResult> UpdatePlanetChannelRouteAsync(
        [FromBody] Channel updated,
        long planetId,
        long channelId,
        ChannelService channelService,
        PlanetMemberService memberService)
    {
        if (updated.Id != channelId)
            return ValourResult.BadRequest("Channel id in body does not match channel id in route");
        
        var old = await channelService.GetPlanetChannelAsync(planetId, channelId);
        if (old is null)
            return ValourResult.NotFound<Channel>();

        if (old.PlanetId is null)
        {
            return ValourResult.BadRequest("Only planet channels can be updated through this endpoint");
        }
        
        // Get the planet member
        var member = await memberService.GetCurrentAsync(old.PlanetId.Value);
        if (member is null)
            return ValourResult.Forbid("You are not a member of this channel");

        if (!await channelService.HasPermissionAsync(old, member, ChatChannelPermissions.ManageChannel))
        {
            return ValourResult.Forbid("You do not have permission to update this channel");
        }

        var result = await channelService.UpdateAsync(updated);
        if (!result.Success)
        {
            return ValourResult.BadRequest(result.Message);
        }

        return ValourResult.Json(result.Data);
    }
    
    [ValourRoute(HttpVerbs.Delete, "api/planets/{planetId}/channels/{channelId}")]
    [UserRequired(UserPermissionsEnum.PlanetManagement)]
    public static async Task<IResult> DeletePlanetChannelRouteAsync(
        long planetId,
        long channelId,
        ChannelService channelService,
        PlanetMemberService memberService)
    {
        var channel = await channelService.GetPlanetChannelAsync(planetId, channelId);
        if (channel is null)
            return ValourResult.NotFound("Channel not found");

        if (channel.PlanetId is null)
        {
            return ValourResult.BadRequest("Only planet channels can be deleted through this endpoint");
        }
        
        var member = await memberService.GetCurrentAsync(channel.PlanetId.Value);
        if (member is null)
            return ValourResult.Forbid("You are not a member of this channel");

        if (!await channelService.HasPermissionAsync(channel, member, ChatChannelPermissions.ManageChannel))
        {
            return ValourResult.Forbid("You do not have permission to delete this channel");
        }

        var result = await channelService.DeletePlanetChannelAsync(planetId, channelId);
        if (!result.Success)
        {
            return ValourResult.BadRequest(result.Message);
        }

        return Results.Ok();
    }

    [ValourRoute(HttpVerbs.Post, "api/planets/{planetId}/channels")]
    [UserRequired]
    public static async Task<IResult> CreatePlanetChannelRouteAsync(
        long planetId,
        [FromBody] CreateChannelRequest request,
        ChannelService channelService,
        TokenService tokenService,
        PlanetMemberService memberService,
        PlanetRoleService roleService)
    {
        var channel = request.Channel;
        
        if (channel is null)
            return ValourResult.BadRequest("Include channel in body");
        
        if (channel.PlanetId != planetId)
            return ValourResult.BadRequest("Channel planet id does not match route planet id");
        
        var token = await tokenService.GetCurrentTokenAsync();
        
        // Planet channel fun
        if (channel.PlanetId is null)
        {
            return ValourResult.BadRequest("Only planet channels can be created through this endpoint");
        }
        
        var member = await memberService.GetCurrentAsync(channel.PlanetId.Value);
        
        if (!await memberService.HasPermissionAsync(member, PlanetPermissions.CreateChannels))
        {
            return ValourResult.BadRequest("You do not have permission to create channels");
        }
        
        // Check permission for the category we are inserting into
        if (channel.ParentId is not null)
        {
            var parent = await channelService.GetPlanetChannelAsync(planetId, channel.ParentId.Value);
            if (parent is null || parent.ChannelType != ChannelTypeEnum.PlanetCategory)
            {
                return ValourResult.BadRequest("Invalid parent id");
            }

            if (!await memberService.HasPermissionAsync(member, parent, CategoryPermissions.ManageCategory))
            {
                return ValourResult.BadRequest("You do not have permission to insert into this category");
            }
        }

        if (request.Nodes is not null && request.Nodes.Count > 0)
        {
            var memberAuthority = await memberService.GetAuthorityAsync(member);
            
            foreach (var node in request.Nodes)
            {
                var role = await roleService.GetAsync(planetId, node.RoleId);
                if (memberAuthority < role.GetAuthority())
                {
                    return ValourResult.Forbid("A permission node's role cannot have higher authority than the member creating it");
                }
            }
        }
        
        var result = await channelService.CreateAsync(request.Channel, request.Nodes);
        if (!result.Success)
        {
            return ValourResult.BadRequest(result.Message);
        }
        
        return Results.Json(result.Data);
    }
    
    [ValourRoute(HttpVerbs.Get, "api/channels/direct/{otherUserId}")]
    [UserRequired(UserPermissionsEnum.DirectMessages)]
    public static async Task<IResult> GetDirectRouteAsync(
        long otherUserId,
        ChannelService channelService,
        UserService userService,
        [FromQuery] bool create = true)
    {
        var userId = await userService.GetCurrentUserIdAsync();
        
        var channel = await channelService.GetDirectChatAsync(userId, otherUserId, create);
        if (channel is null)
            return ValourResult.NotFound<Channel>();

        return Results.Json(channel);
    }

    [ValourRoute(HttpVerbs.Get, "api/channels/direct/self")] 
    [UserRequired(UserPermissionsEnum.DirectMessages)]
    public static async Task<IResult> GetAllDirectRouteAsync(
        ChannelService channelService,
        TokenService tokenService)
    {
        var token = await tokenService.GetCurrentTokenAsync();
        var channels = await channelService.GetAllDirectAsync(token.UserId);
        
        if (channels is null)
            channels = new List<Channel>();

        return Results.Json(channels);
    }

    [ValourRoute(HttpVerbs.Get, "api/planets/{planetId}/channels/{channelId}/children")]
    [UserRequired]
    public static async Task<IResult> GetChildrenAsync(
        long planetId,
        long channelId,
        ChannelService channelService,
        PlanetMemberService memberService)
    {
        var channel = await channelService.GetPlanetChannelAsync(planetId, channelId);
        if (channel is null)
            return ValourResult.NotFound("Channel not found");

        if (channel.ChannelType != ChannelTypeEnum.PlanetCategory)
            return Results.Json(Array.Empty<long>());

        var member = await memberService.GetCurrentAsync(channel.PlanetId!.Value);
        if (member is null)
            return ValourResult.NotPlanetMember();
        
        var children = await channelService.GetChildrenIdsAsync(channelId);
        return Results.Json(children);
    }
    
    [ValourRoute(HttpVerbs.Get, "api/channels/{channelId}/nodes")]
    [UserRequired]
    public static async Task<IResult> GetNodesAsync(
        long channelId,
        ChannelService channelService,
        PlanetMemberService memberService)
    {
        var channel = await channelService.GetPlanetChannelAsync(channelId);
        if (channel is null)
            return ValourResult.NotFound("Channel not found");

        if (channel.ChannelType != ChannelTypeEnum.PlanetCategory)
            return Results.Json(Array.Empty<PermissionsNode>());

        var member = await memberService.GetCurrentAsync(channel.PlanetId!.Value);
        if (member is null)
            return ValourResult.NotPlanetMember();
        
        var nodes = await channelService.GetPermissionNodesAsync(channelId);
        return Results.Json(nodes);
    }

    [ValourRoute(HttpVerbs.Get, "api/channels/direct/{channelId}/members")]
    [UserRequired]
    public static async Task<IResult> GetDirectChannelMembersAsync(
        long channelId,
        ChannelService channelService,
        TokenService tokenService)
    {
        var token = await tokenService.GetCurrentTokenAsync();
        
        var channel = await channelService.GetDirectChatAsync(channelId);
        
        if (!await channelService.HasAccessAsync(channelId, token.UserId))
            return ValourResult.Forbid("You are not a member of this channel");

        return Results.Json(await channelService.GetDirectChannelMembersAsync(channelId));
    }
    
    [ValourRoute(HttpVerbs.Post, "api/channels/{id}/typing")]
    [UserRequired(UserPermissionsEnum.Messages)]
    public static async Task<IResult> PostTypingAsync(
        long id, 
        CurrentlyTypingService typingService,
        ChannelService channelService,
        TokenService tokenService)
    {
        var token = await tokenService.GetCurrentTokenAsync();
        
        // Get the channel
        var channel = await channelService.GetPlanetChannelAsync(id);
        if (channel is null)
            return ValourResult.NotFound("Channel not found");

        if (!await channelService.HasAccessAsync(channel, token.UserId))
        {
            return ValourResult.Forbid("You are not a member of this channel");
        }

        typingService.AddCurrentlyTyping(id, token.UserId);
        
        return Results.Ok();
    }

    [ValourRoute(HttpVerbs.Post, "api/channels/{id}/state")]
    [UserRequired]
    public static async Task<IResult> UpdateStateAsync(
        long id,
        [FromBody] UpdateUserChannelStateRequest request,
        ChannelStateService stateService,
        ChannelService channelService,
        TokenService tokenService)
    {
        var token = await tokenService.GetCurrentTokenAsync();
        
        var channel = await channelService.GetPlanetChannelAsync(id);
        if (channel is null)
            return ValourResult.NotFound("Channel not found");
        
        if (!await channelService.HasAccessAsync(channel, token.UserId))
        {
            return ValourResult.Forbid("You are not a member of this channel");
        }

        var updated = await stateService.UpdateUserChannelState(id, token.UserId, request.UpdateTime);

        return ValourResult.Json(updated);
    }
    
    [ValourRoute(HttpVerbs.Get, "api/channels/{channelId}/messages")]
    [UserRequired(UserPermissionsEnum.Messages)]
    public static async Task<IResult> GetMessagesAsync(
        long channelId,
        MessageService messageService,
        ChannelService channelService,
        TokenService tokenService,
        long index = long.MaxValue,
        int count = 10)
    {
        if (count > 64)
            return Results.BadRequest("Maximum count is 64.");
        
        var token = await tokenService.GetCurrentTokenAsync();
        var channel = await channelService.GetPlanetChannelAsync(channelId);
        
        if (!await channelService.HasAccessAsync(channel, token.UserId))
            return ValourResult.Forbid("You are not a member of this channel");
        
        if (channel.PlanetId is null)
        {
            if (!token.HasScope(UserPermissions.DirectMessages))
            {
                return ValourResult.Forbid("Token lacks permission to delete messages in this channel");
            }
        }
        
        var messages = await messageService.GetChannelMessagesAsync(channelId, count, index);
        
        return Results.Json(messages);
    }
}