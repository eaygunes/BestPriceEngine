using System;
using BestPriceEngine.Repos;
using System.Data;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.SqlClient;
using Dapper;
using System.Diagnostics;

namespace BestPriceEngine.Managers
{
    public sealed class PriceRepoManager : IProductPriceRepoManager
    {
        private const int MillisecondsInSecond = 1000;

        private readonly Dictionary<int, PriceRepo> productIdToPriceRepoDict = new();
        private readonly ILogger<Worker> logger;
        private readonly IConfiguration configuration;
        private readonly string sqlConnStr;

        public PriceRepoManager(ILogger<Worker> logger, IConfiguration configuration)
        {
            this.logger = logger;
            this.configuration = configuration;

            sqlConnStr = this.configuration.GetSection("ConnectionStrings").GetValue<string>("SqlServer");

            if (string.IsNullOrWhiteSpace(sqlConnStr))
            {
                throw new ArgumentException("Fatal Error - Sql Connection String cannot be null.");
            }
        }

        public int GetRepoCount()
        {
            return productIdToPriceRepoDict.Count;
        }
        
        public PriceRepo GetRepoFromProductId(int productId)
        {
            if (!productIdToPriceRepoDict.TryGetValue(productId, out PriceRepo? priceRepo))
            {
                priceRepo = new PriceRepo(productId, logger, configuration);
                productIdToPriceRepoDict.Add(productId, priceRepo);
            }

            return priceRepo;
        }

        public void InvalidateBestPricesInDb()
        {
            using var connection = new SqlConnection(sqlConnStr);
            connection.Open();

            var sqlQueryDeleteAllRecords = SqlQueries.BestPricesTableDeleteAllRecordsQuery;

            var count = connection.Execute(sqlQueryDeleteAllRecords);
            logger.LogInformation("InvalidateBestPricesInDb: Deleted record count: {}", count);
        }

        public void UpdateBestPricesInDbForAllRepos()
        {
            DataTable table = new(SqlQueries.BestPricesTable);

            DataColumn idColumn = new()
            {
                DataType = System.Type.GetType("System.Int32"),
                AutoIncrement = true,
                ColumnName = "Id"
            };
            table.Columns.Add(idColumn);

            DataColumn productIdColumn = new()
            {
                DataType = System.Type.GetType("System.Int32"),
                ColumnName = "ProductId"
            };
            table.Columns.Add(productIdColumn);

            DataColumn priceMaxColumn = new()
            {
                DataType = System.Type.GetType("System.Double"),
                ColumnName = "PriceMax"
            };
            table.Columns.Add(priceMaxColumn);

            DataColumn priceMinColumn = new()
            {
                DataType = System.Type.GetType("System.Double"),
                ColumnName = "PriceMin"
            };
            table.Columns.Add(priceMinColumn);

            DataColumn listingCountColumn = new()
            {
                DataType = System.Type.GetType("System.Int32"),
                ColumnName = "ListingCount"
            };

            table.Columns.Add(listingCountColumn);

            DataColumn updateTimeColumn = new()
            {
                DataType = System.Type.GetType("System.DateTime"),
                ColumnName = "UpdateTime"
            };
            table.Columns.Add(updateTimeColumn);


            DataColumn isDeletedColumn = new()
            {
                DataType = System.Type.GetType("System.Boolean"),
                ColumnName = "IsDeleted"
            };

            table.Columns.Add(isDeletedColumn);


            var now = DateTime.Now;

            foreach (var repo in productIdToPriceRepoDict.Values)
            {
                DataRow row = table.NewRow();
                row[productIdColumn.ColumnName] = repo.ProductId;
                row[priceMaxColumn.ColumnName] = repo.MaxPrice;
                row[priceMinColumn.ColumnName] = repo.MinPrice;
                row[listingCountColumn.ColumnName] = repo.GetListingCount();
                row[updateTimeColumn.ColumnName] = now;
                row[isDeletedColumn.ColumnName] = false;
                table.Rows.Add(row);
            }
            logger.LogInformation("UpdateBestPricesInDbForAllRepos: Prepared data table for bulk SQL operation");


            logger.LogInformation("UpdateBestPricesInDbForAllRepos: Pushing the data table to DB.");
            Stopwatch stopwatch = new();
            stopwatch.Start();

            using var connection = new SqlConnection(sqlConnStr);
            connection.Open();
            SqlBulkCopy sbc = new(connection)
            {
                DestinationTableName = SqlQueries.BestPricesTable
            };
            sbc.WriteToServer(table);

            stopwatch.Stop();
            logger.LogInformation("UpdateBestPricesInDbForAllRepos: Data push to DB completed in {} seconds.", stopwatch.ElapsedMilliseconds / MillisecondsInSecond);
        }
    }
}
