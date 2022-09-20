using BestPriceEngine;
using BestPriceEngine.Managers;

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddSingleton<IProductPriceRepoManager, PriceRepoManager>();
        services.AddSingleton<IEventConsumerManager, EventConsumerManager>();

        services.AddHostedService<Worker>();
    })
    .Build();

await host.RunAsync();
