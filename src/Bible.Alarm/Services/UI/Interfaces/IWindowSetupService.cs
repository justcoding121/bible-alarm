using Microsoft.Maui.Controls;

namespace Bible.Alarm.Services.UI.Interfaces;

public interface IWindowSetupService : IDisposable
{
    Window CreateWindow(IActivationState? activationState);
    void TearDown();
}
