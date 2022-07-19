using Valour.Server.Database;

namespace Valour.Server.Workers;

/*  Valour - A free and secure chat client
 *  Copyright (C) 2021 Vooper Media LLC
 *  This program is subject to the GNU Affero General Public license
 *  A copy of the license should be included - if not, see <http://www.gnu.org/licenses/>
 */

public class MessageCacheWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    public readonly ILogger<MessageCacheWorker> _logger;

    public MessageCacheWorker(ILogger<MessageCacheWorker> logger,
                        IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            Task task = Task.Run(async () =>
            {
                while (true)
                {
                    try
                    {
                        using (var scope = _scopeFactory.CreateScope())
                        {
                            ValourDB context = scope.ServiceProvider.GetRequiredService<ValourDB>();
                            DateTime now = DateTime.UtcNow;
                            var cutOff = now.AddHours(-24);

                            context.PlanetMessages.RemoveRange(
                                context.PlanetMessages.Where(x => x.TimeSent < cutOff).OrderBy(x => x.TimeSent));

                            await context.SaveChangesAsync();
                        }
                        Console.WriteLine("Checked Message Cache");

                        await Task.Delay(1000 * 60 * 60);
                    }
                    catch (System.Exception e)
                    {
                        Console.WriteLine("FATAL MESSAGE CACHE ERROR:");
                        Console.WriteLine(e.Message);
                    }
                }
            });

            while (!task.IsCompleted)
            {
                _logger.LogInformation("Message Cache Worker running at: {time}", DateTimeOffset.Now);
                await Task.Delay(60000, stoppingToken);
            }

            _logger.LogInformation("Message Cache Worker task stopped at: {time}", DateTimeOffset.Now);
            _logger.LogInformation("Restarting.", DateTimeOffset.Now);
        }
    }
}
