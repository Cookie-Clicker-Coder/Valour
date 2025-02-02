using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using StackExchange.Redis;
using Valour.Config.Configs;
using Valour.Sdk.Models.Messages.Embeds;
using Valour.Server.Hubs;
using Valour.Shared.Channels;
using Valour.Shared.Models;
using Notification = Valour.Server.Models.Notification;
using Planet = Valour.Server.Models.Planet;
using User = Valour.Server.Models.User;
using UserChannelState = Valour.Server.Models.UserChannelState;

namespace Valour.Server.Services;

public class CoreHubService
{
    // Map of channelids to users typing from prev channel update
    public static ConcurrentDictionary<long, List<long>> PrevCurrentlyTyping = new ConcurrentDictionary<long, List<long>>();
    
    private readonly IHubContext<CoreHub> _hub;
    private readonly ValourDb _db;
    private readonly IServiceProvider _serviceProvider;
    private readonly IConnectionMultiplexer _redis;

    public CoreHubService(ValourDb db, IServiceProvider serviceProvider, IHubContext<CoreHub> hub, IConnectionMultiplexer redis)
    {
        _db = db;
        _hub = hub;
        _serviceProvider = serviceProvider;
        _redis = redis;
    }
    
    public async Task RelayMessage(Message message)
    {
        var groupId = $"c-{message.ChannelId}";

        // Group we are sending messages to
        var group = _hub.Clients.Group(groupId);

        if (ConnectionTracker.GroupConnections.ContainsKey(groupId)) {
            // All of the connections to this group
            var viewingIds = ConnectionTracker.GroupUserIds[groupId];
            
            await _db.Database.ExecuteSqlRawAsync("CALL batch_user_channel_state_update({0}, {1}, {2});", 
                viewingIds, message.ChannelId, DateTime.UtcNow);
        }

        if (NodeConfig.Instance.LogInfo)
            Console.WriteLine($"[{NodeConfig.Instance.Name}]: Relaying message {message.Id} to group {groupId}");

        await group.SendAsync("Relay", message);
    }
    
    public async void RelayMessageEdit(Message message)
    {
        var groupId = $"c-{message.ChannelId}";

        // Group we are sending messages to
        var group = _hub.Clients.Group(groupId);
        
        if (NodeConfig.Instance.LogInfo)
            Console.WriteLine($"[{NodeConfig.Instance.Name}]: Relaying edited message {message.Id} to group {groupId}");

        await group.SendAsync("RelayEdit", message);
    }

    public async Task RelayFriendEvent(long targetId, FriendEventData eventData, NodeLifecycleService nodeLifecycleService)
    {
        await nodeLifecycleService.RelayUserEventAsync(targetId, NodeLifecycleService.NodeEventType.Friend, eventData);
    }

    public async Task RelayDirectMessage(Message message, NodeLifecycleService nodeLifecycleService, List<long> userIds)
    {
        foreach (var userId in userIds)
        {
            await nodeLifecycleService.RelayUserEventAsync(userId, NodeLifecycleService.NodeEventType.DirectMessage, message);
        }
    }
    
    public async Task RelayDirectMessageEdit(Message message, NodeLifecycleService nodeLifecycleService, List<long> userIds)
    {
        foreach (var userId in userIds)
        {
            await nodeLifecycleService.RelayUserEventAsync(userId, NodeLifecycleService.NodeEventType.DirectMessageEdit, message);
        }
    }

    public async void RelayNotification(Notification notif, NodeLifecycleService nodeLifecycleService)
    {
        await nodeLifecycleService.RelayUserEventAsync(notif.UserId, NodeLifecycleService.NodeEventType.Notification, notif);
    }
    
    public async void RelayNotificationReadChange(Notification notif, NodeLifecycleService nodeLifecycleService)
    {
        await nodeLifecycleService.RelayUserEventAsync(notif.UserId, NodeLifecycleService.NodeEventType.Notification, notif);
    }
    
    public async void RelayNotificationsCleared(long userId, NodeLifecycleService nodeLifecycleService)
    {
        await nodeLifecycleService.RelayUserEventAsync(userId, NodeLifecycleService.NodeEventType.NotificationsCleared, userId);
    }
    
    public async void NotifyCategoryOrderChange(CategoryOrderEvent eventData) =>
        await _hub.Clients.Group($"p-{eventData.PlanetId}").SendAsync("CategoryOrder-Update", eventData);
    
    public async void NotifyRoleOrderChange(RoleOrderEvent eventData) =>
        await _hub.Clients.Group($"p-{eventData.PlanetId}").SendAsync("RoleOrder-Update", eventData);

    public async void NotifyUserChannelStateUpdate(long userId, UserChannelState state) =>
        await _hub.Clients.Group($"u-{userId}").SendAsync("UserChannelState-Update", state);

    public async void NotifyPlanetItemChange<T>(long planetId, T model, int flags = 0) =>
        await _hub.Clients.Group($"p-{planetId}").SendAsync($"{typeof(T).Name}-Update", model, flags);
    
    public async void NotifyPlanetItemChange<T>(T model, int flags = 0) where T : ISharedPlanetModel =>
        await _hub.Clients.Group($"p-{model.PlanetId}").SendAsync($"{typeof(T).Name}-Update", model, flags);

    public async void NotifyPlanetItemDelete<T>(T model) where T : ISharedPlanetModel =>
        await _hub.Clients.Group($"p-{model.PlanetId}").SendAsync($"{typeof(T).Name}-Delete", model);
    
    public async void NotifyPlanetItemDelete<T>(long planetId, T model) =>
        await _hub.Clients.Group($"p-{planetId}").SendAsync($"{typeof(T).Name}-Delete", model);

    public async void NotifyPlanetChange(Planet item, int flags = 0) =>
        await _hub.Clients.Group($"p-{item.Id}").SendAsync($"{nameof(Planet)}-Update", item, flags);

    public async void NotifyPlanetDelete(Planet item) =>
        await _hub.Clients.Group($"p-{item.Id}").SendAsync($"{nameof(Planet)}-Delete", item);
    
    public async void NotifyInteractionEvent(EmbedInteractionEvent interaction) =>
        await _hub.Clients.Group($"i-{interaction.PlanetId}").SendAsync("InteractionEvent", interaction);

    public async void NotifyMessageDeletion(Message message) =>
        await _hub.Clients.Group($"c-{message.ChannelId}").SendAsync("DeleteMessage", message);

    public async void NotifyDirectMessageDeletion(Message message, long targetUserId) =>
        await _hub.Clients.Group($"u-{targetUserId}").SendAsync("DeleteMessage", message);

    public async void NotifyPersonalEmbedUpdateEvent(PersonalEmbedUpdate u) =>
        await _hub.Clients.Group($"u-{u.TargetUserId}").SendAsync("Personal-Embed-Update", u);

    public async void NotifyChannelEmbedUpdateEvent(ChannelEmbedUpdate u) =>
        await _hub.Clients.Group($"c-{u.TargetChannelId}").SendAsync("Channel-Embed-Update", u);
    
    public async Task NotifyUserChange(User user, int flags = 0)
    {
        // TODO: Get all locally loaded planets and check if user is member; if so, send update
        // we can probably manage this *without* a database call
        
        var planetIds = await _db.PlanetMembers.Where(x => x.UserId == user.Id)
            .Select(x => x.PlanetId)
            .ToListAsync();

        foreach (var id in planetIds)
        {
            // TODO: This will not work with node scaling
            await _hub.Clients.Group($"p-{planetIds}").SendAsync("User-Update", user, flags);
        }
    }

    public async Task NotifyUserDelete(User user)
    {
        var members = await _db.PlanetMembers.Where(x => x.UserId == user.Id).ToListAsync();

        foreach (var m in members)
        {
            await _hub.Clients.Group($"p-{m.PlanetId}").SendAsync("User-Delete", user);
        }
    }
    
    public void UpdateChannelsWatching()
        {
            foreach (var pair in ConnectionTracker.GroupUserIds)
            {
                // Channel connections only
                if (!pair.Key.StartsWith('c'))
                    continue;

                // Send current active channel connection user ids

                // no need to await these
#pragma warning disable CS4014
                var channelid = long.Parse(pair.Key.Substring(2));
                _hub.Clients.Group(pair.Key).SendAsync("Channel-Watching-Update", new ChannelWatchingUpdate
                {
                    ChannelId = channelid,
                    UserIds = pair.Value.Distinct().ToList()
                });
#pragma warning restore CS4014
            }
        }

        public async void NotifyCurrentlyTyping(long channelId, long userId)
        {
            await _hub.Clients.Group($"c-{channelId}").SendAsync("Channel-CurrentlyTyping-Update", new ChannelTypingUpdate
            {
                ChannelId = channelId,
                UserId = userId
            });
        }

        public async void NotifyChannelStateUpdate(long planetId, long channelId, DateTime time)
        {
            await _hub.Clients.Group($"p-{planetId}").SendAsync("Channel-State", new ChannelStateUpdate(channelId, time, planetId));
        }

    ////////////////
    // Eco Events //
    ////////////////

    public async void NotifyPlanetTransactionProcessed(Transaction transaction)
    {
        await _hub.Clients.Group($"p-{transaction.PlanetId}").SendAsync("Transaction-Processed", transaction);
        await _hub.Clients.Group($"u-{transaction.UserFromId}").SendAsync("Transaction-Processed", transaction);
        await _hub.Clients.Group($"u-{transaction.UserToId}").SendAsync("Transaction-Processed", transaction);
    }

    public async Task RelayTransaction(Transaction transaction, NodeLifecycleService nodeLifecycleService)
    {
        await nodeLifecycleService.RelayUserEventAsync(transaction.UserFromId, NodeLifecycleService.NodeEventType.Transaction, transaction);
        await nodeLifecycleService.RelayUserEventAsync(transaction.UserToId, NodeLifecycleService.NodeEventType.Transaction, transaction);
    }

    public async void NotifyCurrencyChange(Currency item, int flags = 0) =>
        await _hub.Clients.Group($"p-{item.Id}").SendAsync($"Currency-Update", item, flags);
}