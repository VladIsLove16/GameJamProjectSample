namespace JamStarter
{
    /// <summary>
    /// Implemented by scene roots that need persistent application services.
    /// AppBootstrap injects the dependencies after the scene is loaded.
    /// </summary>
    public interface IAppServicesConsumer
    {
        void Initialize(AppServices services);
    }
}
