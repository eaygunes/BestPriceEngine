using BestPriceEngine.Models;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dapper;
using BestPriceEngine.Managers;

namespace BestPriceEngine.Repos
{
    public class PriceRepo
    {
        private readonly List<ListingPrice> sortedAscendingPriceList = new();

        private const int FirstIndexOfList = 0;
        private const int EmptyListLength = 0;
        private const double DefaultMinPrice = -1;
        private const double DefaultMaxPrice = -1;

        private static readonly IComparer<ListingPrice> PriceComparator =
          Comparer<ListingPrice>.Create((ListingPrice lp1, ListingPrice lp2) => lp1.Price.CompareTo(lp2.Price));
        private readonly ILogger<Worker> logger;
        private readonly IConfiguration configuration;
        private readonly string sqlConnStr;

        public int ProductId { get; private set; }

        public double MinPrice { get; private set; } = DefaultMinPrice;
        public double MaxPrice { get; private set; } = DefaultMaxPrice;

        public int GetListingCount()
        {
            return sortedAscendingPriceList.Count;
        }

        public PriceRepo(int productId, ILogger<Worker> logger, IConfiguration configuration)
        {
            ProductId = productId;
            this.logger = logger;
            this.configuration = configuration;

            sqlConnStr = this.configuration.GetSection("ConnectionStrings").GetValue<string>("SqlServer");

            if (string.IsNullOrWhiteSpace(sqlConnStr))
            {
                throw new ArgumentException("Fatal Error - Sql Connection String cannot be null.");
            }
        }

        private BestPriceInfo GetBestPriceInfo()
        {
            BestPriceInfo bestPriceInfo = new()
            {
                MaxPrice = this.MaxPrice,
                MinPrice = this.MinPrice,
                ListingCount = this.GetListingCount()
            };

            return bestPriceInfo;
        }

        public void AddNewListingPrice(ListingPrice listingPrice, bool updateDb, bool skipDeletionInDb)
        {
            BestPriceInfo lastBestPriceInfo = GetBestPriceInfo();

            if (sortedAscendingPriceList.Count == EmptyListLength)
            {
                MinPrice = listingPrice.Price;
                MaxPrice = listingPrice.Price;
                sortedAscendingPriceList.Add(listingPrice);
            }
            else
            {
                if (listingPrice.Price > MaxPrice)
                {
                    sortedAscendingPriceList.Add(listingPrice);
                    MaxPrice = listingPrice.Price;
                }
                else if (listingPrice.Price < MinPrice)
                {
                    sortedAscendingPriceList.Insert(FirstIndexOfList, listingPrice);
                    MinPrice = listingPrice.Price;
                }
                else
                {
                    var binarySearchIndexWithBitwiseLogic = sortedAscendingPriceList.BinarySearch(listingPrice, PriceComparator);

                    if (binarySearchIndexWithBitwiseLogic < 0)
                    {
                        binarySearchIndexWithBitwiseLogic = ~binarySearchIndexWithBitwiseLogic;
                    }

                    sortedAscendingPriceList.Insert(binarySearchIndexWithBitwiseLogic, listingPrice);
                }

            }

            if (updateDb)
            {
                if (MaxPrice != lastBestPriceInfo.MaxPrice
                   || MinPrice != lastBestPriceInfo.MinPrice
                   || GetListingCount() != lastBestPriceInfo.ListingCount)
                {
                    UpdateBestPricesInDb(skipDeletionInDb);
                }
            }
        }


        public void DeleteListingPrice(int listingId, bool updateDb)
        {
            BestPriceInfo lastBestPriceInfo = GetBestPriceInfo();
            var index = sortedAscendingPriceList.FindIndex(lp => lp.ListingId == listingId);

            if (index >= 0)
            {
                sortedAscendingPriceList.RemoveAt(index);

                if (sortedAscendingPriceList.Count == 0)
                {
                    MinPrice = DefaultMinPrice;
                    MaxPrice = DefaultMaxPrice;
                }
                else
                {
                    MinPrice = sortedAscendingPriceList[0].Price;
                    MinPrice = sortedAscendingPriceList[sortedAscendingPriceList.Count - 1].Price;
                }
            }

            if (updateDb)
            {
                if (MaxPrice != lastBestPriceInfo.MaxPrice
                   || MinPrice != lastBestPriceInfo.MinPrice
                   || GetListingCount() != lastBestPriceInfo.ListingCount)
                {
                    UpdateBestPricesInDb(false);
                }
            }
        }

        private void UpdateBestPricesInDb(bool skipDeletion)
        {
            using var connection = new SqlConnection(sqlConnStr);
            connection.Open();

            var sqlQueryDeleteOldIfExists = SqlQueries.BestPricesTableDeleteAllRecordsQuery;
            var sqlQueryAddNewBestPrices = SqlQueries.BestPricesTableAddNewBestPricesQuery;

            var sqlQuery = string.Empty;

            if (!skipDeletion)
            {
                sqlQuery += sqlQueryDeleteOldIfExists;
            }

            sqlQuery += sqlQueryAddNewBestPrices;

#pragma warning disable IDE0037 // Use inferred member name
            var count = connection.Execute(sqlQuery,
                new
                {
                    ProductId = this.ProductId,
                    PriceMax = this.MaxPrice,
                    PriceMin = this.MinPrice,
                    ListingCount = GetListingCount(),
                    UpdateTime = DateTime.Now
                });
#pragma warning restore IDE0037 // Use inferred member name

            if (count != 1)
            {
                logger.LogError("AddNewListingPrice - Failed on updating DB for product {}", ProductId);
            }
        }



        public void UpdateListingPrice(ListingPrice listingPrice)
        {
            DeleteListingPrice(listingPrice.ListingId, false);
            AddNewListingPrice(listingPrice, true, false);
        }

    }
}
