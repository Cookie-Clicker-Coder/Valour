﻿using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Valour.Server.Categories;
using Valour.Server.Database;
using Valour.Server.Oauth;
using Valour.Server.Planets;
using Valour.Shared;
using Valour.Shared.Oauth;

namespace Valour.Server.API
{
    public class ChannelAPI
    {
        public static void AddRoutes(WebApplication app)
        {
            app.MapPost("/channel/delete", Delete);
            app.MapPost("/channel/setparent", SetParent);
            app.MapPost("/channel/create", Create);
        }

        /// <summary>
        /// Creates a channel
        /// </summary>

        // Type:
        // POST
        // -----------------------------------
        //
        // Route:
        // /channel/create
        // -----------------------------------
        //
        // Query parameters:
        // ---------------------------------------------------
        // | auth      | Authentication key        | string  |
        // | planet_id | Id of target planet       | ulong   |
        // | parent_id | Id of the parent category | ulong   |
        // | name      | The name for the channel  | string  |
        // ---------------------------------------------------
        private static async Task Create(HttpContext ctx, ValourDB db)
        {
            // Request parameter validation //

            if (!ctx.Request.Query.TryGetValue("token", out var token))
            {
                ctx.Response.StatusCode = 401;
                await ctx.Response.WriteAsync("Include token");
                return;
            }

            if (!ctx.Request.Query.TryGetValue("planet_id", out var planet_id_in))
            {
                ctx.Response.StatusCode = 400;
                await ctx.Response.WriteAsync("Include planet_id");
                return;
            }

            if (!ctx.Request.Query.TryGetValue("parent_id", out var parent_id_in))
            {
                ctx.Response.StatusCode = 400;
                await ctx.Response.WriteAsync("Include channel_id");
                return;
            }

            if (!ctx.Request.Query.TryGetValue("name", out var name))
            {
                ctx.Response.StatusCode = 401;
                await ctx.Response.WriteAsync("Include name");
                return;
            }

            ulong planet_id;
            bool planet_id_parse = ulong.TryParse(planet_id_in, out planet_id);

            if (!planet_id_parse)
            {
                ctx.Response.StatusCode = 400;
                await ctx.Response.WriteAsync("Could not parse planet_id");
                return;
            }

            ulong parent_id;
            bool parent_id_parse = ulong.TryParse(parent_id_in, out parent_id);

            if (!parent_id_parse)
            {
                ctx.Response.StatusCode = 400;
                await ctx.Response.WriteAsync("Could not parse parent_id");
                return;
            }

            TaskResult name_valid = ServerPlanetChatChannel.ValidateName(name);

            if (!name_valid.Success)
            {
                ctx.Response.StatusCode = 400;
                await ctx.Response.WriteAsync($"Name is not valid [name: {name}]");
                return;
            }

            // Request authorization //

            AuthToken auth = await ServerAuthToken.TryAuthorize(token, db);

            if (!auth.HasScope(UserPermissions.PlanetManagement))
            {
                ctx.Response.StatusCode = 401;
                await ctx.Response.WriteAsync("Token lacks UserPermissions.PlanetManagement scope");
                return;
            }

            ServerPlanet planet = await db.Planets.Include(x => x.Members.Where(x => x.User_Id == auth.User_Id))
                                                  .FirstOrDefaultAsync(x => x.Id == planet_id);

            var member = planet.Members.FirstOrDefault();

            if (!await planet.HasPermissionAsync(member, PlanetPermissions.ManageChannels, db))
            {
                ctx.Response.StatusCode = 401;
                await ctx.Response.WriteAsync("Member lacks PlanetPermissions.ManageChannels node");
                return;
            }

            // Request action //

            // Creates the channel

            ServerPlanetChatChannel channel = new ServerPlanetChatChannel()
            {
                Id = IdManager.Generate(),
                Name = name,
                Planet_Id = planet_id,
                Parent_Id = parent_id,
                Message_Count = 0,
                Description = "A chat channel",
                Position = (ushort)(await db.PlanetChatChannels.CountAsync(x => x.Parent_Id == parent_id))
            };

            // Add channel to database
            await db.PlanetChatChannels.AddAsync(channel);

            // Save changes to DB
            await db.SaveChangesAsync();

            // Send channel refresh
            PlanetHub.NotifyChatChannelChange(channel);

            ctx.Response.StatusCode = 200;
            await ctx.Response.WriteAsync(channel.Id.ToString());
        }

        /// <summary>
        /// Sets the parent of the given channel
        /// </summary>

        // Type:
        // POST
        // -----------------------------------
        // Route:
        // /channel/setparent
        //
        // -----------------------------------
        //
        // Query parameters:
        // --------------------------------------------------
        // | auth       | Authentication key       | string |
        // | channel_id | Id of target channel     | ulong  |
        // | parent_id  | Id of the parent channel | ulong  |
        // --------------------------------------------------

        private static async Task SetParent(HttpContext ctx, ValourDB db)
        {
            // Request parameter validation //

            if (!ctx.Request.Query.TryGetValue("token", out var token))
            {
                ctx.Response.StatusCode = 401;
                await ctx.Response.WriteAsync("Include token");
                return;
            }

            if (!ctx.Request.Query.TryGetValue("channel_id", out var channel_id_in))
            {
                ctx.Response.StatusCode = 400;
                await ctx.Response.WriteAsync("Include channel_id");
                return;
            }

            if (!ctx.Request.Query.TryGetValue("parent_id", out var parent_id_in))
            {
                ctx.Response.StatusCode = 400;
                await ctx.Response.WriteAsync("Include channel_id");
                return;
            }

            ulong channel_id;
            bool channel_id_parse = ulong.TryParse(channel_id_in, out channel_id);

            if (!channel_id_parse)
            {
                ctx.Response.StatusCode = 400;
                await ctx.Response.WriteAsync("Could not parse channel_id");
                return;
            }

            ulong parent_id;
            bool parent_id_parse = ulong.TryParse(parent_id_in, out parent_id);

            if (!parent_id_parse)
            {
                ctx.Response.StatusCode = 400;
                await ctx.Response.WriteAsync("Could not parse parent_id");
                return;
            }

            // Request authorization //

            AuthToken auth = await ServerAuthToken.TryAuthorize(token, db);

            if (!auth.HasScope(UserPermissions.PlanetManagement))
            {
                ctx.Response.StatusCode = 401;
                await ctx.Response.WriteAsync("Token lacks UserPermissions.PlanetManagement scope");
                return;
            }

            ServerPlanetChatChannel channel = await db.PlanetChatChannels.Include(x => x.Planet)
                                                                         .ThenInclude(x => x.Members.Where(x => x.User_Id == auth.User_Id))
                                                                         .FirstOrDefaultAsync(x => x.Id == channel_id);

            if (channel == null)
            {
                ctx.Response.StatusCode = 400;
                await ctx.Response.WriteAsync($"Could not find channel [id: {channel_id}]");
                return;
            }

            ServerPlanetMember member = channel.Planet.Members.FirstOrDefault();

            if (!await channel.Planet.HasPermissionAsync(member, PlanetPermissions.ManageChannels, db))
            {
                ctx.Response.StatusCode = 401;
                await ctx.Response.WriteAsync($"Member lacks PlanetPermissions.ManageChannels");
                return;
            }

            // Request action //

            // If parent does not exist or does not belong to the same planet
            if (!await db.PlanetCategories.AnyAsync(x => x.Id == parent_id && x.Planet_Id == channel.Planet_Id))
            {
                ctx.Response.StatusCode = 400;
                await ctx.Response.WriteAsync($"Parent does not exist or belongs to another planet [id: {parent_id}]");
                return;
            }

            // Fulfill request
            channel.Parent_Id = parent_id;

            await db.SaveChangesAsync();

            // Notify of change
            PlanetHub.NotifyChatChannelChange(channel);

            ctx.Response.StatusCode = 200;
            await ctx.Response.WriteAsync("Success");
        }

        /// <summary>
        /// Deletes the given channel
        /// </summary>

        // Type:
        // POST
        // -----------------------------------
        //
        // Route:
        // /channel/delete
        // -----------------------------------
        //
        // Query parameters:
        // ----------------------------------------------
        // | auth       | Authentication key   | string |
        // | channel_id | Id of target channel | ulong  |
        // ----------------------------------------------

        private static async Task Delete(HttpContext ctx, ValourDB db)
        {
            // Request parameter validation //
            
            if (!ctx.Request.Query.TryGetValue("token", out var token))
            {
                ctx.Response.StatusCode = 401;
                await ctx.Response.WriteAsync("Include token");
                return;
            }

            if (!ctx.Request.Query.TryGetValue("channel_id", out var channel_id_in))
            {
                ctx.Response.StatusCode = 400;
                await ctx.Response.WriteAsync("Include channel_id");
                return;
            }

            ulong channel_id;
            bool channel_id_parse = ulong.TryParse(channel_id_in, out channel_id);

            if (!channel_id_parse)
            {
                ctx.Response.StatusCode = 400;
                await ctx.Response.WriteAsync($"Could not parse channel_id");
                return;
            }

            // Request authorization //

            ServerAuthToken auth = await ServerAuthToken.TryAuthorize(token, db);

            if (auth == null)
            {
                ctx.Response.StatusCode = 401;
                await ctx.Response.WriteAsync($"Token is invalid [token: {token}]");
                return;
            }

            ServerPlanetChatChannel channel = await db.PlanetChatChannels.Include(x => x.Planet)
                                                                         .ThenInclude(x => x.Members.Where(x => x.User_Id == auth.User_Id))
                                                                         .FirstOrDefaultAsync(x => x.Id == channel_id);

            if (channel == null)
            {
                ctx.Response.StatusCode = 400;
                await ctx.Response.WriteAsync($"Could not find channel with [id: {channel_id}]");
                return;
            }

            // We should have ONLY loaded in the target member
            ServerPlanetMember member = channel.Planet.Members.FirstOrDefault();

            if (!await channel.Planet.HasPermissionAsync(member, PlanetPermissions.ManageChannels, db))
            {
                ctx.Response.StatusCode = 401;
                await ctx.Response.WriteAsync($"Could not authorize member for [node: ManageChannels]");
                return;
            }

            if (channel_id == channel.Planet.Main_Channel_Id)
            {
                ctx.Response.StatusCode = 400;
                await ctx.Response.WriteAsync($"You cannot delete a planet's main channel [id: {channel_id}]");
                return;
            }

            // Request action //

            // Remove permission nodes
            db.ChatChannelPermissionsNodes.RemoveRange(
                db.ChatChannelPermissionsNodes.Where(x => x.Channel_Id == channel_id)
            );

            // Remove messages
            db.PlanetMessages.RemoveRange(
                db.PlanetMessages.Where(x => x.Channel_Id == channel_id)
            );

            // Remove channel
            db.PlanetChatChannels.Remove(channel);

            // Save changes
            await db.SaveChangesAsync();

            // Notify channel deletion
            await PlanetHub.NotifyChatChannelDeletion(channel);

            ctx.Response.StatusCode = 200;
            await ctx.Response.WriteAsync($"Removed channel [id: {channel_id}]");
        }
    }
}
