using BestPriceEngine.Managers;

namespace BestPriceEngine
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> logger;
        private readonly IEventConsumerManager eventConsumerManager;

        public Worker(IEventConsumerManager eventConsumerManager, ILogger<Worker> logger)
        {
            this.eventConsumerManager = eventConsumerManager;
            this.logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Task.Delay(1000, stoppingToken);

            logger.LogInformation("Price Engine started at: {time}", DateTimeOffset.Now);
            eventConsumerManager.ProcessInitialDataFromDb();

            while (!stoppingToken.IsCancellationRequested)
            {
                logger.LogInformation("Processing data updates at: {time}", DateTimeOffset.Now);
                eventConsumerManager.ProcessDataUpdatesFromDb();
                await Task.Delay(1000, stoppingToken);
            }
        }



    }
}