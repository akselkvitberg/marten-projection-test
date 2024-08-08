using System.Diagnostics;
using Marten;

namespace MartenProjectionDuplicateDetection;

public static class RandomBox
{
    public static string Id = Guid.NewGuid().ToString();
}

public class Writer(ILogger<Writer> logger, IServiceProvider serviceProvider) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken cancellationToken)    {
        var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(10));

        await CreateBox();

        for (var i = 0; i < 100000; i++)
        {
            await timer.WaitForNextTickAsync(cancellationToken);

            using var scope = serviceProvider.CreateScope();
            await using var session = scope.ServiceProvider.GetRequiredService<IDocumentSession>();
            
            session.Events.Append(RandomBox.Id , new AddItem(i));

            if (Random.Shared.Next(50) == 0)
            {
                //logger.LogInformation("Adding duplicate {Id} to box", i);
                session.Events.Append(RandomBox.Id, new AddItem(i));
            }
            
            if (Random.Shared.Next(500) == 0)
            {
                logger.LogInformation("Deleting box", i);
                session.Events.Append(RandomBox.Id, new DeleteBox());
            }
            
            
            
            await session.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task CreateBox()
    {
        using var scope = serviceProvider.CreateScope();
        await using var session = scope.ServiceProvider.GetRequiredService<IDocumentSession>();
            
        session.Events.Append(RandomBox.Id , new BoxCreated());
        await session.SaveChangesAsync();
    }
}

public class Reader(ILogger<Reader> logger, IServiceProvider serviceProvider) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));

        for (var i = 0; i < 100000; i++)
        {
            await timer.WaitForNextTickAsync(cancellationToken);
            
            using var scope = serviceProvider.CreateScope();
            await using var session = scope.ServiceProvider.GetRequiredService<IDocumentSession>();
            
            var sw = Stopwatch.StartNew();
            var aggregate = await session.Events.AggregateStreamAsync<BoxAggregate>(RandomBox.Id, token: cancellationToken);
            sw.Stop();
            var aggregateTime = sw.ElapsedMilliseconds;
            
            sw.Restart();
            var projection1 = await session.LoadAsync<BoxSimple>(RandomBox.Id, token:cancellationToken);
            sw.Stop();
            var projectionTime1 = sw.Elapsed.TotalNanoseconds;
            
            sw.Restart();
            var projection2 = await session.LoadAsync<BoxFull>(RandomBox.Id, token:cancellationToken);
            sw.Stop();
            var projectionTime2 = sw.Elapsed.TotalNanoseconds;
            
            sw.Restart();
            var projection3 = await session.Query<BoxFull>().Select(x=> new BoxSimple(){Id = x.Id, Duplicates = x.Duplicates, Items = x.Items.Count}).FirstOrDefaultAsync(simple => simple.Id == RandomBox.Id, token: cancellationToken);
            sw.Stop();
            var projectionTime3 = sw.Elapsed.TotalNanoseconds;
            
            logger.LogInformation("Aggregate contains: {Count} items, duplicates: {Duplicates}, Projection simple contains: {Count2} items, duplicates: {Duplicates2}, Projection full contains: {Count3} items, duplicates: {Duplicates3}, Projection query contains: {Count4} items, duplicates: {Duplicates4}", 
                aggregate?.Items.Count, aggregate?.Duplicates, 
                projection1?.Items, projection1?.Duplicates, 
                projection2?.Items.Count, projection2?.Duplicates,
                projection3?.Items, projection3?.Duplicates);

            //if (aggregateTime + projectionTime1 + projectionTime2 > 200)
            {
                logger.LogWarning("Elapsed time - Aggregate: {AggregateTime} ms, Projection Simple: {Projection1Time} ns, Projection Full: {Projection2Time} ns, Projection Query: {Projection3Time} ns", 
                    aggregateTime, projectionTime1, projectionTime2, projectionTime3);
            }
        }
    }
}