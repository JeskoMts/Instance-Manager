namespace InstanceManager.ViewModels;

public interface IAppLifetime
{
    bool IsIdle { get; }

    void InstallUpdateOnExit();

    void RestartAfterUpdate();
}
