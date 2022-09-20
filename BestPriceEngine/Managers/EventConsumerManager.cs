using BestPriceEngine.Models;
using System.Data.SqlClient;
using Dapper;

namespace BestPriceEngine.Managers
{
    public class EventConsumerManager : IEventConsumerManager
    {
        private readonly IProductPriceRepoManager productPriceRepoManager;
        private readonly ILogger<Worker> logger;
        private readonly IConfiguration configuration;
        private readonly Random random = new();

        private readonly string sqlConnStr;

        //private string statusText = string.Empty;
        private int lastProcessedChangeTrackingVersion = 0;

        public EventConsumerManager(IProductPriceRepoManager productPriceRepoManager, ILogger<Worker> logger, IConfiguration configuration)
        {
            this.productPriceRepoManager = productPriceRepoManager;
            this.logger = logger;
            this.configuration = configuration;
            sqlConnStr = this.configuration.GetSection("ConnectionStrings").GetValue<string>("SqlServer");

            if (string.IsNullOrWhiteSpace(sqlConnStr))
            {
                throw new ArgumentException("Fatal Error - Sql Connection String cannot be null.");
            }
        }

        /// <summary>
        /// https://learn.microsoft.com/en-us/sql/relational-databases/system-functions/changetable-transact-sql?view=sql-server-ver16#changetable-version
        /// </summary>
        /// <param name="connection"></param>
        /// <returns></returns>
        private static int GetCurrentChangeTrackingVersion(SqlConnection connection)
        {
            IEnumerable<dynamic> result = connection.Query(SqlQueries.ChangeTrackingGetCurrentVersionQuery);
            var currentVersion = (int)result.First().CurrentVersion;
            return currentVersion;
        }

        public void ProcessInitialDataFromDb()
        {
            using var connection = new SqlConnection(sqlConnStr);
            connection.Open();

            var currentVersion = GetCurrentChangeTrackingVersion(connection);
            logger.LogInformation("ProcessInitialDataFromDb: CurrentVersion: {}", currentVersion);

            var priceEventsForInitialData = connection.Query<PriceEvent>(SqlQueries.InitialDataQuery);
            logger.LogInformation("ProcessInitialDataFromDb: Obtained price events from DB.");


            productPriceRepoManager.InvalidateBestPricesInDb();
            logger.LogInformation("ProcessInitialDataFromDb: Invalidated old best prices in DB");

            foreach (var priceEvent in priceEventsForInitialData)
            {
                var repo = productPriceRepoManager.GetRepoFromProductId(priceEvent.ProductId);
                repo.AddNewListingPrice(new ListingPrice { ListingId = priceEvent.ListingId, Price = priceEvent.Price }, false, true);
            }

            logger.LogInformation("ProcessInitialDataFromDb: Calculated best prices in memory for {} products.", productPriceRepoManager.GetRepoCount());

            productPriceRepoManager.UpdateBestPricesInDbForAllRepos();
            logger.LogInformation("ProcessInitialDataFromDb: Pushed all best prices to DB.");

            this.lastProcessedChangeTrackingVersion = currentVersion;
        }


        public void ProcessDataUpdatesFromDb()
        {
            using var connection = new SqlConnection(sqlConnStr);
            connection.Open();

            var currentVersion = GetCurrentChangeTrackingVersion(connection);

            if (currentVersion <= this.lastProcessedChangeTrackingVersion)
            {
                logger.LogInformation("ProcessDataUpdatesFromDb: There are no new updates - last Processed version is the current version: {}",
                    lastProcessedChangeTrackingVersion);
                return;
            }

            logger.LogDebug("ProcessDataUpdatesFromDb: CurrentVersion: {}", currentVersion);

            var sqlChangeTrackingDeltaSinceLastCheckQuery = String.Format(SqlQueries.DataUpdateQueryTemplate, this.lastProcessedChangeTrackingVersion);
            var sqlChangeTrackingRows = connection.Query<SqlChangeTrackingRow>(sqlChangeTrackingDeltaSinceLastCheckQuery);
            logger.LogDebug("ProcessDataUpdatesFromDb: ChangeTrackingOperation to EventType conversion starts.");

            var now = DateTime.Now;
            var count = 0;

            var priceEventList = new List<PriceEvent>();

            foreach (var cte in sqlChangeTrackingRows)
            {
                var priceEvent = ConvertSqlChangeTrackingRowToPriceEvent(cte, now);

                if (priceEvent != null)
                {
                    priceEventList.Add(priceEvent);
                }

                count++;
            }

            logger.LogDebug("ProcessDataUpdatesFromDb: SqlChangeTracking to PriceEvent conversion finished for {} events. Fail count: {}",
                count, count - priceEventList.Count);

            ProcessDataUpdateEvents(priceEventList);

            this.lastProcessedChangeTrackingVersion = currentVersion;

            logger.LogInformation("ProcessDataUpdatesFromDb finished. CurrentVersion: {} , EventCount{}", currentVersion, count);
        }

        private PriceEvent? ConvertSqlChangeTrackingRowToPriceEvent(SqlChangeTrackingRow ctRow, DateTime createDate)
        {
            PriceEvent priceEvent = new()
            {
                ProductId = ctRow.ProductId,
                ListingId = ctRow.ListingId,
                Price = ctRow.Price,
                CreateDate = createDate
            };

            //https://learn.microsoft.com/en-us/sql/relational-databases/system-functions/changetable-transact-sql?view=sql-server-ver16#changetable-changes
            switch (ctRow.ChangeTrackingOperation)
            {
                case 'U':
                    if (ctRow.IsDeleted == 0 && ctRow.IsActive == 1)
                    {
                        priceEvent.EventType = EventTypeEnum.Update;
                    }
                    else
                    {
                        priceEvent.EventType = EventTypeEnum.Delete;
                    }
                    break;
                case 'I':
                    priceEvent.EventType = EventTypeEnum.Insert;
                    break;
                case 'D':
                    priceEvent.EventType = EventTypeEnum.Delete;
                    break;
                default:
                    logger.LogError("ConvertSqlChangeTrackingRowToPriceEvent: Unsupported Change Tracking Operation Type: {@item}", ctRow);
                    return null;
            }

            return priceEvent;
        }

        private void ProcessDataUpdateEvents(IEnumerable<PriceEvent> eventList)
        {
            var countSuccess = 0;
            var countFailure = 0;

            foreach (var priceEvent in eventList)
            {
                var repo = productPriceRepoManager.GetRepoFromProductId(priceEvent.ProductId);

                switch (priceEvent.EventType)
                {
                    case EventTypeEnum.Insert:
                        repo.AddNewListingPrice(new ListingPrice { ListingId = priceEvent.ListingId, Price = priceEvent.Price }, true, false);
                        countSuccess++;
                        break;
                    case EventTypeEnum.Update:
                        repo.UpdateListingPrice(new ListingPrice { ListingId = priceEvent.ListingId, Price = priceEvent.Price });
                        countSuccess++;
                        break;
                    case EventTypeEnum.Delete:
                        repo.DeleteListingPrice(priceEvent.ListingId, true);
                        countSuccess++;
                        break;
                    default:
                        logger.LogError("ProcessDataUpdateEvents: Unsupported event type: {@item}", priceEvent);
                        countFailure++;
                        break;
                }
            }

            logger.LogInformation("ProcessDataUpdateEvents: Finished. Success count: {}, Fail count: {}", countSuccess, countFailure);
        }

    }
}
