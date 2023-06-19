using Microsoft.AspNetCore.Mvc;
using Valour.Server.Database;
using Valour.Server.Requests;
using Valour.Shared.Authorization;
using Valour.TenorTwo.Models;

namespace Valour.Server.Api.Dynamic;

public class PlanetCategoryApi
{
    [ValourRoute(HttpVerbs.Get, "api/categories/{id}")]
    [UserRequired(UserPermissionsEnum.Membership)]
    public static async Task<IResult> GetRouteAsync(
        long id,
        PlanetCategoryService service,
        PlanetMemberService memberService)
    {
        // Get the category
        var category = await service.GetAsync(id);
        if (category is null)
            return ValourResult.NotFound("Category not found");

        // Get member
        var member = await memberService.GetCurrentAsync(category.PlanetId);
        if (member is null)
            return ValourResult.NotPlanetMember();

        // Ensure member has permission to view this category
        if (!await memberService.HasPermissionAsync(member, category, CategoryPermissions.View))
            return ValourResult.LacksPermission(CategoryPermissions.View);

        // Return json
        return Results.Json(category);
    }

    [ValourRoute(HttpVerbs.Post, "api/categories")]
    [UserRequired(UserPermissionsEnum.PlanetManagement)]
    public static async Task<IResult> PostRouteAsync(
        [FromBody] PlanetCategory category,
        PlanetCategoryService categoryService,
        PlanetMemberService memberService)
    {
        if (category is null)
            return ValourResult.BadRequest("Include category in body.");

        category.Id = IdManager.Generate();

        // Get member
        var member = await memberService.GetCurrentAsync(category.PlanetId);
        if (member is null)
            return ValourResult.NotPlanetMember();

        if (category.ParentId is not null)
        {
            // Ensure user has permission for parent category management
            var parent = await categoryService.GetAsync((long)category.ParentId);
            if (!await memberService.HasPermissionAsync(member, parent, CategoryPermissions.ManageCategory))
                return ValourResult.LacksPermission(CategoryPermissions.ManageCategory);
        }
        else
        {
            if (!await memberService.HasPermissionAsync(member, PlanetPermissions.CreateChannels))
                return ValourResult.LacksPermission(PlanetPermissions.CreateChannels);
        }

        var result = await categoryService.CreateAsync(category);
        if (!result.Success)
            return ValourResult.Problem(result.Message);

        return Results.Created($"api/categories/{result.Data.Id}", result.Data);

    }

    [ValourRoute(HttpVerbs.Post, "api/categories/detailed")]
    [UserRequired(UserPermissionsEnum.PlanetManagement)]
    public static async Task<IResult> PostRouteWithDetailsAsync(
        [FromBody] CreatePlanetCategoryChannelRequest request,
        PlanetMemberService memberService,
        PlanetCategoryService categoryService,
        PlanetService planetService)
    {
        if (request is null)
            return ValourResult.BadRequest("Include request in body.");

        request.Category.Id = IdManager.Generate();

        // Get member
        var member = await memberService.GetCurrentAsync(request.Category.PlanetId);
        if (member is null)
            return ValourResult.NotPlanetMember();

        var planet = await planetService.GetAsync(request.Category.PlanetId);

        if (request.Category.ParentId is not null)
        {
            // Ensure user has permission for parent category management
            var parent = await categoryService.GetAsync((long)request.Category.ParentId);
            if (!await memberService.HasPermissionAsync(member, parent, CategoryPermissions.ManageCategory))
                return ValourResult.LacksPermission(CategoryPermissions.ManageCategory);
        }
        else
        {
            if (!await memberService.HasPermissionAsync(member, PlanetPermissions.CreateChannels))
                return ValourResult.LacksPermission(PlanetPermissions.CreateChannels);
        }

        var result = await categoryService.CreateDetailedAsync(request, member);
        if (!result.Success)
            return ValourResult.Problem(result.Message);

        return Results.Created($"api/categories/{result.Data.Id}", result.Data);
    }


    [ValourRoute(HttpVerbs.Put, "api/categories/{id}")]
    [UserRequired(UserPermissionsEnum.PlanetManagement)]
    public static async Task<IResult> PutRouteAsync(
        [FromBody] PlanetCategory category,
        long id,
        PlanetMemberService memberService,
        PlanetCategoryService categoryService)
    {
        // Get the category
        var old = await categoryService.GetAsync(id);
        if (old is null)
            return ValourResult.NotFound("Category not found");

        // Get member
        var member = await memberService.GetCurrentAsync(old.PlanetId);
        if (member is null)
            return ValourResult.NotPlanetMember();

        if (!await memberService.HasPermissionAsync(member, old, CategoryPermissions.ManageCategory))
            return ValourResult.LacksPermission(CategoryPermissions.ManageCategory);

        var result = await categoryService.UpdateAsync(category);
        if (!result.Success)
            return ValourResult.Problem(result.Message);

        return Results.Json(category);
    }

    [ValourRoute(HttpVerbs.Delete, "api/categories/{id}")]
    [UserRequired(UserPermissionsEnum.PlanetManagement)]
    public static async Task<IResult> DeleteRouteAsync(
        long id,
        PlanetCategoryService categoryService,
        PlanetMemberService memberService)
    {
        // Get the category
        var category = await categoryService.GetAsync(id);
        if (category is null)
            return ValourResult.NotFound("Category not found");

        // Get member
        var member = await memberService.GetCurrentAsync(category.PlanetId);
        if (member is null)
            return ValourResult.NotPlanetMember();

        if (!await memberService.HasPermissionAsync(member, category, CategoryPermissions.ManageCategory))
            return ValourResult.LacksPermission(CategoryPermissions.ManageCategory);

        if (await categoryService.IsLastCategory(category))
            return Results.BadRequest("Last category cannot be deleted.");

        var childCount = await categoryService.GetChildCountAsync(id);

        if (childCount > 0)
            return Results.BadRequest("Category must be empty.");

        await categoryService.DeleteAsync(category);

        return Results.NoContent();
    }

    [ValourRoute(HttpVerbs.Get, "api/categories/{id}/children")]
    [UserRequired(UserPermissionsEnum.Membership)]
    public static async Task<IResult> GetChildrenRouteAsync(
        long id,
        PlanetCategoryService categoryService,
        PlanetMemberService memberService)
    {
        // Get the channel
        var category = await categoryService.GetAsync(id);
        if (category is null)
            return ValourResult.NotFound("Category not found");

        // Get member
        var member = await memberService.GetCurrentAsync(category.PlanetId);
        if (member is null)
            return ValourResult.NotPlanetMember();

        if (!await memberService.HasPermissionAsync(member, category, CategoryPermissions.View))
            return ValourResult.LacksPermission(CategoryPermissions.View);

        return Results.Json(await categoryService.GetChildrenIdsAsync(category.Id));
    }


    [ValourRoute(HttpVerbs.Post, "api/categories/{id}/children/insert/{insertId}/{position}")]
    [UserRequired(UserPermissionsEnum.PlanetManagement)]
    public static async Task<IResult> InsertChildRouteAsync(
        long insertId,
        long id,
        PlanetCategoryService categoryService,
        PlanetMemberService memberService,
        PlanetChannelService channelService,
        int position = -1)
    {
        // Get the category
        var category = await categoryService.GetAsync(id);
        if (category is null)
            return ValourResult.NotFound("Category not found");

        // Get member
        var member = await memberService.GetCurrentAsync(category.PlanetId);
        if (member is null)
            return ValourResult.NotPlanetMember();

        if (!await memberService.HasPermissionAsync(member, category, CategoryPermissions.ManageCategory))
            return ValourResult.LacksPermission(CategoryPermissions.ManageCategory);
        
        // If the child currently belongs to another category (not planet), we need to check permissions for it
        var inserting = await channelService.GetAsync(insertId);
        if (inserting.ParentId == id)
            return ValourResult.BadRequest("Channel is already in this category.");
        
        // We need to get the old category and ensure we have permissions in it
        if (inserting.ParentId is not null)
        {
            var oldCategory = await categoryService.GetAsync(inserting.ParentId.Value);
            if (!await memberService.HasPermissionAsync(member, oldCategory, CategoryPermissions.ManageCategory))
                return ValourResult.LacksPermission(CategoryPermissions.ManageCategory);
        }
        
        // We have permission for the insert, the target category, and the old category if applicable.
        // Actually do the changes.
        await categoryService.InsertChildAsync(category.Id, inserting.Id, position);

        return ValourResult.Ok("Success");
    }

    [ValourRoute(HttpVerbs.Post, "api/categories/{id}/children/order")]
    [UserRequired(UserPermissionsEnum.PlanetManagement)]
    public static async Task<IResult> SetChildOrderRouteAsync(
        [FromBody] long[] order,
        long id,
        PlanetCategoryService categoryService,
        PlanetMemberService memberService,
        PlanetChannelService channelService)
    {
        // Get the category
        var category = await categoryService.GetAsync(id);
        if (category is null)
            return ValourResult.NotFound("Category not found");

        // Get member
        var member = await memberService.GetCurrentAsync(category.PlanetId);
        if (member is null)
            return ValourResult.NotPlanetMember();

        if (!await memberService.HasPermissionAsync(member, category, CategoryPermissions.ManageCategory))
            return ValourResult.LacksPermission(CategoryPermissions.ManageCategory);

        order = order.Distinct().ToArray();


        // We have to check permissions for ALL changes in this ordering. Fuuuuuuun!
        var pos = 0;
        foreach (var childId in order)
        {
            var child = await channelService.GetAsync(childId);
            if (child is null)
                return ValourResult.NotFound($"Child {childId} not found");

            if (child.ParentId != id)
                return ValourResult.BadRequest("Use the category insert route to change parent id");
            
            // Change in permission or parent requires perms
            if (child.Position != pos || child.ParentId != category.Id)
            {
                // Require permission for the child being moved
                if (!await memberService.HasPermissionAsync(member, child, ChannelPermissions.Manage))
                    return ValourResult.LacksPermission(ChannelPermissions.Manage);
            }
            
            pos++;
        }
        
        // Actually do the changes
        var result = await categoryService.SetChildOrderAsync(category, order);
        if (!result.Success)
            return ValourResult.Problem(result.Message);

        return Results.NoContent();
    }

    [ValourRoute(HttpVerbs.Get, "api/categories/{id}/nodes")]
    [UserRequired]
    public static async Task<IResult> GetNodesRouteAsync(long id, PlanetChannelService service)
    {
        return Results.Json(await service.GetPermNodesAsync(id));
    }
}