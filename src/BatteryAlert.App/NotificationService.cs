using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using System.Media;

namespace BatteryAlert.App;

public sealed class NotificationService : IDisposable
{
    private bool _registered;
    public string? RegistrationError { get; private set; }
    public event Action? OpenRequested;

    public NotificationService()
    {
        try
        {
            AppNotificationManager.Default.NotificationInvoked += Invoked;
            AppNotificationManager.Default.Register();
            _registered = true;
        }
        catch (Exception ex) { RegistrationError = "Notificările Windows nu sunt disponibile: " + ex.Message; }
    }

    private void Invoked(AppNotificationManager sender, AppNotificationActivatedEventArgs args) => OpenRequested?.Invoke();

    public void Show(string message, bool sound)
    {
        if (!_registered) throw new InvalidOperationException(RegistrationError);
        var notification = new AppNotificationBuilder()
            .AddArgument("action", "open")
            .AddText("Baterie scăzută")
            .AddText(message)
            .MuteAudio().BuildNotification();
        AppNotificationManager.Default.Show(notification);
        if (notification.Id == 0) throw new IOException("Windows nu a acceptat notificarea.");
        if (sound) PlaySound();
    }

    public static void PlaySound() => SystemSounds.Exclamation.Play();

    public void Dispose()
    {
        if (!_registered) return;
        AppNotificationManager.Default.NotificationInvoked -= Invoked;
        AppNotificationManager.Default.Unregister();
        _registered = false;
    }
}
