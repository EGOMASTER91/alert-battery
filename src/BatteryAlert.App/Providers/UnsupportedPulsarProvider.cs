namespace BatteryAlert.App.Providers;

// Inventory unknown Pulsar IDs without sending guessed commands to them.
public sealed class UnsupportedPulsarProvider : IBatteryProvider
{
    public string Name => "Pulsar — detectare";
    public Task<IReadOnlyList<DeviceDescriptor>> DiscoverAsync(CancellationToken ct) => Task.Run<IReadOnlyList<DeviceDescriptor>>(() =>
    {
        return HidInventory.Enumerate()
            .Where(e => e.Device.VendorID is 0x3710 or 0x3554)
            .Where(e => !PulsarModelCatalog.Supported(e.Device.VendorID, e.Device.ProductID))
            .GroupBy(e => e.Device.DevicePath.Split('#').ElementAtOrDefault(2) ?? e.Identity)
            .Select(g => g.First())
            .Select(e => new DeviceDescriptor("unsupported:" + e.Identity, e.Product, Name, e.HardwareId, e.Device.DevicePath))
            .ToArray();
    }, ct);
    public Task<BatteryReading> ReadAsync(DeviceDescriptor device, CancellationToken ct) =>
        Task.FromResult(new BatteryReading(device, null, null, Availability.Unsupported, DateTimeOffset.UtcNow,
            "Citirea bateriei nu este suportată — este necesară validarea protocolului acestui model"));
}
