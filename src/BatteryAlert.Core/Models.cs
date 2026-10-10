namespace BatteryAlert.Core;

public enum Availability { Available, Unavailable, Unsupported }
public enum DeviceKind { Unknown, Headset, Mouse, Keyboard, Other }
public enum WirelessCapability { Unknown, Confirmed, WiredOnly }
public enum ConnectionKind { Unknown, Bluetooth, UsbReceiver, UsbCable }

public sealed record DeviceDescriptor(string Id, string Name, string Provider,
    string? HardwareId = null, string? Path = null,
    DeviceKind Kind = DeviceKind.Unknown, WirelessCapability Wireless = WirelessCapability.Unknown,
    ConnectionKind Connection = ConnectionKind.Unknown, string? Evidence = null,
    string? PhysicalId = null, string[]? LegacyIds = null);

public sealed record BatteryReading(DeviceDescriptor Device, int? Percent,
    bool? Charging, Availability Availability, DateTimeOffset ReadAt, string? Detail = null)
{
    public bool IsValid => Availability == Availability.Available && Percent is >= 0 and <= 100;
    public static BatteryReading Unavailable(DeviceDescriptor device, string? detail = null) =>
        new(device, null, null, Availability.Unavailable, DateTimeOffset.UtcNow, detail);
}

public interface IBatteryProvider
{
    string Name { get; }
    Task<IReadOnlyList<DeviceDescriptor>> DiscoverAsync(CancellationToken cancellationToken);
    Task<BatteryReading> ReadAsync(DeviceDescriptor device, CancellationToken cancellationToken);
}

public sealed class DeviceSettings
{
    public bool Enabled { get; set; } = true;
    public int Threshold { get; set; } = 10;
    public bool Desktop { get; set; } = true;
    public bool Phone { get; set; } = true;
    public bool Sound { get; set; } = true;
    public string? ProductImageFile { get; set; }
    public DeviceSettings Clone() => (DeviceSettings)MemberwiseClone();
}

public sealed class AppSettings
{
    public bool Sound { get; set; } = true;
    public bool StartWithWindows { get; set; }
    public bool PhoneEnabled { get; set; }
    public string NtfyServer { get; set; } = "https://ntfy.sh";
    public string NtfyTopic { get; set; } = "battery-" + Guid.NewGuid().ToString("N");
    public Dictionary<string, DeviceSettings> Devices { get; set; } = [];
    public DeviceSettings For(string id)
    {
        if (!Devices.TryGetValue(id, out var settings)) Devices[id] = settings = new();
        return settings;
    }
    public AppSettings Clone() => new()
    {
        Sound = Sound, StartWithWindows = StartWithWindows, PhoneEnabled = PhoneEnabled,
        NtfyServer = NtfyServer, NtfyTopic = NtfyTopic,
        Devices = Devices.ToDictionary(pair => pair.Key, pair => pair.Value.Clone())
    };
}

public sealed class AlertState
{
    public bool Alerted { get; set; }
    public int Threshold { get; set; } = 10;
}

public sealed record LowBatteryAlert(string Id, string DeviceId, string DeviceName,
    int Percent, DateTimeOffset CreatedAt)
{
    public string Message => $"{DeviceName} — baterie {Percent}%";
}

public sealed class PendingNotification
{
    public required LowBatteryAlert Alert { get; set; }
    public int Attempts { get; set; }
    public DateTimeOffset NextAttempt { get; set; }
}

public sealed class SavedState
{
    public int Version { get; set; } = 1;
    public AppSettings Settings { get; set; } = new();
    public Dictionary<string, AlertState> Alerts { get; set; } = [];
    public List<PendingNotification> Pending { get; set; } = [];
}
