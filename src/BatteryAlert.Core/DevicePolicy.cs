namespace BatteryAlert.Core;

public static class DevicePolicy
{
    public static bool Visible(DeviceDescriptor device) => device.Wireless == WirelessCapability.Confirmed
        && device.Kind is DeviceKind.Headset or DeviceKind.Mouse or DeviceKind.Keyboard;

    public static string Category(DeviceKind kind) => kind switch
    {
        DeviceKind.Headset => "Căști wireless", DeviceKind.Mouse => "Mouse wireless",
        DeviceKind.Keyboard => "Tastatură wireless", DeviceKind.Other => "Alt dispozitiv", _ => "Categorie neconfirmată"
    };
    public static string Connection(ConnectionKind connection) => connection switch
    {
        ConnectionKind.Bluetooth => "Bluetooth", ConnectionKind.UsbReceiver => "Receiver USB · wireless",
        ConnectionKind.UsbCable => "Cablu USB", _ => "Conexiune neconfirmată"
    };
    public static string Exclusion(DeviceDescriptor device) => device.Wireless == WirelessCapability.WiredOnly
        ? "Exclusiv cu fir · exclus din monitorizare" : device.Kind == DeviceKind.Other
        ? "Categoria nu este monitorizată" : !Visible(device)
        ? "Wireless sau categoria nu sunt confirmate" : "Wireless confirmat";

    // A physical identity is supplied only when the provider has positive evidence.
    // Category prevents merging independent functions of a composite device.
    public static IReadOnlyList<BatteryReading> Merge(IEnumerable<BatteryReading> readings) => readings
        .GroupBy(r => r.Device.PhysicalId == null ? "id:" + r.Device.Id : $"physical:{r.Device.PhysicalId}:{r.Device.Kind}")
        .Select(group =>
        {
            var best = group.OrderByDescending(r => Visible(r.Device))
                .ThenByDescending(r => r.IsValid).ThenByDescending(r => r.Charging.HasValue)
                .ThenByDescending(r => r.Availability != Availability.Unsupported).First();
            var id = best.Device.PhysicalId == null ? best.Device.Id : $"physical:{best.Device.PhysicalId}:{best.Device.Kind}";
            var aliases = group.SelectMany(r => (r.Device.LegacyIds ?? []).Append(r.Device.Id))
                .Where(alias => alias != id).Distinct().ToArray();
            return best with { Device = best.Device with { Id = id, LegacyIds = aliases } };
        }).ToArray();

    public static void PreserveState(SavedState state, DeviceDescriptor device)
    {
        var aliases = device.LegacyIds ?? [];
        if (!state.Settings.Devices.ContainsKey(device.Id))
        {
            var old = aliases.FirstOrDefault(state.Settings.Devices.ContainsKey);
            if (old != null) state.Settings.Devices[device.Id] = state.Settings.Devices[old].Clone();
        }
        var oldAlert = aliases.Select(id => state.Alerts.GetValueOrDefault(id)).FirstOrDefault(a => a?.Alerted == true);
        if (oldAlert != null && !state.Alerts.ContainsKey(device.Id))
            state.Alerts[device.Id] = new() { Alerted = oldAlert.Alerted, Threshold = oldAlert.Threshold };
        foreach (var pending in state.Pending.Where(p => aliases.Contains(p.Alert.DeviceId)))
            pending.Alert = pending.Alert with { DeviceId = device.Id };
        var duplicates = state.Pending.Where(p => p.Alert.DeviceId == device.Id).OrderByDescending(p => p.Alert.CreatedAt).Skip(1).ToArray();
        foreach (var duplicate in duplicates) state.Pending.Remove(duplicate);
    }

    public static DeviceKind BluetoothClass(uint raw) => ((raw >> 8) & 31, (raw >> 2) & 63) switch
    {
        (5, var minor) when (minor & 16) != 0 && (minor & 32) == 0 => DeviceKind.Keyboard,
        (5, var minor) when (minor & 32) != 0 && (minor & 16) == 0 => DeviceKind.Mouse,
        (4, 1 or 2 or 6) => DeviceKind.Headset,
        (2 or 3, _) => DeviceKind.Other,
        (5, var minor) when (minor & 3) is 1 or 2 => DeviceKind.Other,
        _ => DeviceKind.Unknown
    };
    public static DeviceKind BleAppearance(ushort appearance) => appearance switch
    { 0x03C1 => DeviceKind.Keyboard, 0x03C2 => DeviceKind.Mouse, 0x03C3 or 0x03C4 => DeviceKind.Other, _ => DeviceKind.Unknown };

    public static bool TryBlePercent(ReadOnlySpan<byte> value, out int percent)
    { percent = value.Length == 1 ? value[0] : -1; return percent is >= 0 and <= 100; }
}
