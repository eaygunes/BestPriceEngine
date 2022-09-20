using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BestPriceEngine.Managers
{
    public static class SqlQueries
    {
        public const string InitialDataQuery = 
            @"SELECT ProductId, Id as 'ListingId', Price 
            FROM [Advertisements] 
            WHERE IsDeleted = 0 and IsActive = 1 
            ORDER BY ProductId asc, Price asc;";

        public const string DataUpdateQueryTemplate =
            @"SELECT A.ProductId, C.Id as 'ListingId', A.Price, A.IsDeleted, A.IsActive, SYS_CHANGE_OPERATION as 'ChangeTrackingOperation' 
            FROM CHANGETABLE (CHANGES [Advertisements], {0}) AS C 
            LEFT JOIN Advertisements as A 
            ON C.Id = A.Id 
            ORDER BY A.ProductId asc, A.Price asc;";

        public const string BestPricesTable = "BestProductPrices";

        public const string BestPricesTableDeleteAllRecordsQuery = "UPDATE [BestProductPrices] SET IsDeleted = 1 where IsDeleted = 0;";

        public const string BestPricesTableAddNewBestPricesQuery = @"INSERT INTO [BestProductPrices] ([ProductId], [PriceMax], [PriceMin], [ListingCount], [UpdateTime], [IsDeleted]) 
                            VALUES (@ProductId, @PriceMax, @PriceMin, @ListingCount, @UpdateTime, 0);";

        public const string ChangeTrackingGetCurrentVersionQuery = "Select CHANGE_TRACKING_CURRENT_VERSION () as 'CurrentVersion';";


    }
}
