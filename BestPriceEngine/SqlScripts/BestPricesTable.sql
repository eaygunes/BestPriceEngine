CREATE TABLE [dbo].[BestProductPrices](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[ProductId] [int] NOT NULL,
	[PriceMax] [decimal] (18,2) NOT NULL,
	[PriceMin] [decimal] (18,2) NOT NULL,
	[ListingCount] [int] NOT NULL,
	[UpdateTime] [datetime2](7) NOT NULL,
	[IsDeleted] [bit] NOT NULL,
) ON [PRIMARY]
GO
