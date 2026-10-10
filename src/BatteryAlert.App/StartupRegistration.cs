using Microsoft.Win32;

namespace BatteryAlert.App;

public static class StartupRegistration
{
    public static void Apply(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
        if (enabled)
        {
            var path = Environment.ProcessPath ?? throw new IOException("Calea aplicației nu este disponibilă.");
            key.SetValue("BatteryAlert", $"\"{path}\" --background");
        }
        else key.DeleteValue("BatteryAlert", false);
    }
}
