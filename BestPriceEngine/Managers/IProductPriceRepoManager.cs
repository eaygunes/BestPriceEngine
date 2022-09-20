using BestPriceEngine.Repos;

namespace BestPriceEngine.Managers
{
    public interface IProductPriceRepoManager
    {
        PriceRepo GetRepoFromProductId(int productId);

        void InvalidateBestPricesInDb();

        void UpdateBestPricesInDbForAllRepos();

        int GetRepoCount();
    }
}