using Marten;
using Marten.Events;
using Marten.Events.Daemon.Resiliency;
using Marten.Events.Projections;
using MartenProjectionDuplicateDetection;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddHostedService<Writer>();
builder.Services.AddHostedService<Reader>();

builder.Services.AddMarten(options =>
{
    options.Connection("Server=localhost;Port=5432;User id=postgres;Password=postgres;Database=postgres");
    options.Events.StreamIdentity = StreamIdentity.AsString;
    options.Projections.Add<BoxFullProjection>(ProjectionLifecycle.Async);
    options.Projections.Add<BoxSimpleProjection>(ProjectionLifecycle.Async);
})
.AddAsyncDaemon(builder.Environment.IsDevelopment() ? DaemonMode.Solo : DaemonMode.HotCold)
.ApplyAllDatabaseChangesOnStartup()
.UseLightweightSessions();

var host = builder.Build();
host.Run();
