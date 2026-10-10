namespace BatteryAlert.App.Providers;

// Inventory only: never sends vendor reports to an unidentified peripheral.
public sealed class CompatibilityProvider : IBatteryProvider
{
    public string Name => "Inventar HID";
    public async Task<IReadOnlyList<DeviceDescriptor>> DiscoverAsync(CancellationToken ct)
    {
        var nodes = await WindowsInventory.SnapshotAsync(ct);
        var result = new List<DeviceDescriptor>();
        foreach (var endpoint in HidInventory.Enumerate())
        {
            ct.ThrowIfCancellationRequested();
            var node = WindowsInventory.ForHid(nodes, endpoint);
            var kind = endpoint.UsagePage == 1 ? endpoint.Usage switch
                { 2 => DeviceKind.Mouse, 6 => DeviceKind.Keyboard, _ => node?.Kind ?? DeviceKind.Unknown }
                : node?.Kind ?? DeviceKind.Unknown;
            if (kind == DeviceKind.Unknown && endpoint.Device.VendorID is not (0x3710 or 0x3554)) continue;
            // Known protocol collections own these devices; receiver mouse/keyboard
            // emulation does not imply a second independent battery.
            if (PulsarModelCatalog.Supported(endpoint.Device.VendorID, endpoint.Device.ProductID)) continue;
            var logitechReceiver = HidppProtocol.IsReceiver(endpoint.Device.VendorID, endpoint.Device.ProductID);
            if (logitechReceiver) kind = DeviceKind.Unknown;
            var keyboard = PulsarModelCatalog.IsKeyboardReceiver(endpoint.Device.VendorID, endpoint.Device.ProductID);
            var physical = WindowsInventory.Physical(node);
            result.Add(new("inventory:" + (physical ?? endpoint.Identity) + ":" + kind, endpoint.Product, Name,
                endpoint.HardwareId, endpoint.Device.DevicePath, keyboard ? DeviceKind.Keyboard : kind,
                keyboard || logitechReceiver ? WirelessCapability.Confirmed : WirelessCapability.Unknown,
                keyboard || logitechReceiver ? ConnectionKind.UsbReceiver : ConnectionKind.Unknown,
                keyboard ? "Receiver de tastatură wireless din catalogul oficial Pulsar" : logitechReceiver
                    ? "Receiver Logitech identificat; perifericele se identifică separat prin HID++" : "HID identificat; USB nu confirmă capacitatea wireless",
                physical));
        }
        return result.DistinctBy(d => d.Id).ToArray();
    }
    public Task<BatteryReading> ReadAsync(DeviceDescriptor device, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(new BatteryReading(device, null, null, Availability.Unsupported, DateTimeOffset.UtcNow,
            DevicePolicy.Visible(device) ? "Citirea bateriei nu este suportată" : DevicePolicy.Exclusion(device)));
    }
}
