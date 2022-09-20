namespace BestPriceEngine.Managers
{
    public interface IEventConsumerManager
    {
        void ProcessInitialDataFromDb();
        void ProcessDataUpdatesFromDb();
    }
}