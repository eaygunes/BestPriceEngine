# BestPriceEngine
Price Aggregator Engine for Product Listings based on SQL Server with Change Tracking.

This engine pre-calculates multiple aggregations of large tables (the implementation contains a generic E-commerce Advertisements table and provides aggregations such as Max Price or Min Price) during data insertion so that those aggregations can be provided instantly on demand, when needed (either from a summary table on SQL server or directly from App Memory).

Thanks to the aggregation engine approach, you can avoid querying large tables to calculate aggregations such as Min, Max or Count.

The engine always keeps the aggregations up to date by using SQL Server's Change Tracking feature. This is useful when you cannot use triggers or when you don't have an event driven architecture (otherwise you can substitute event consumer code for Change Tracking parts).

## Runtime phases:
- On startup, engine loads entire Advertisement table, and processes it.
- Afterwards, each second, engine queries Change Tracking table to get the updates since the last query, and then processes those updates.
- Once engine processes updates, it puts the aggregation values into BestPrices table in the DB.

## Prerequisites:
- Sql Server has to be used as DB.
- Change Tracking has to be enabled on DB and also on the Advertisements table (now column tracking needed).


## Code Organization:
- `Worker` orchestrates the high level operations (Process Initial Data and Process Data Updates).
- `EventConsumerManager` deals with getting initial and also update data from DB and propagating them to `PriceRepoManager` or `PriceRepo`.
- `PriceRepoManager` is used as a registry of `PriceRepo`s. It also handles bulk write operation (during initial data processing) by using `SqlBulkCopy` so that Engine startup is fast (100Ks of records processed under a minute on on 1 CPU container against AWS T2 Micro SQL Server (RDS) instance).
- `PriceRepo` is used for performing actual aggregations in memory. It also updates the summary table on the DB. It uses a sorted list along with binary search to perform fast aggregations. 

Before using the code, please review and update the following:
- SQL queries on `SqlQueries.cs`
- SQL Server Connecting String on `appsettings.json`
