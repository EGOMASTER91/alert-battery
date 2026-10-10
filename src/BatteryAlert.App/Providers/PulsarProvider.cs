using System.Diagnostics;

namespace BatteryAlert.App.Providers;

public sealed class PulsarProvider(bool crazyLight) : IBatteryProvider
{
    private readonly Dictionary<string, HidEndpoint> _devices = [];
    public string Name => crazyLight ? "Pulsar CrazyLight" : "Pulsar Fusion";

    public async Task<IReadOnlyList<DeviceDescriptor>> DiscoverAsync(CancellationToken ct)
    {
      var nodes = await WindowsInventory.SnapshotAsync(ct);
      return await Task.Run<IReadOnlyList<DeviceDescriptor>>(() =>
    {
        ct.ThrowIfCancellationRequested();
        _devices.Clear();
        var result = new List<DeviceDescriptor>();
        foreach (var endpoint in HidInventory.Enumerate())
        {
            var device = endpoint.Device;
            var supported = crazyLight ? PulsarModelCatalog.CrazyLight(device.VendorID, device.ProductID)
                : PulsarModelCatalog.Fusion(device.VendorID, device.ProductID);
            // Restrict output writes to the documented vendor configuration collection.
            if (!supported || endpoint.UsagePage != 0xff02 || endpoint.Usage != 0x0002
                || endpoint.InputLength < 17 || endpoint.OutputLength < 17) continue;
            var id = $"pulsar:{endpoint.Identity}";
            _devices[id] = endpoint;
            var name = !endpoint.Product.StartsWith("USB ", StringComparison.Ordinal) ? "Pulsar " + endpoint.Product.Trim()
                : device.VendorID == 0x3554 ? "Pulsar Fusion (X2 v2 / X2A / Xlite v3)"
                : device.VendorID == 0x25a7 ? "Pulsar X2 Wireless" : "Pulsar CrazyLight / Bibimbap";
            result.Add(PulsarModelCatalog.Classify(new(id, name, Name, endpoint.HardwareId, device.DevicePath,
                PhysicalId: WindowsInventory.Physical(WindowsInventory.ForHid(nodes, endpoint))), device.VendorID, device.ProductID));
        }
        return result;
      }, ct);
    }

    public Task<BatteryReading> ReadAsync(DeviceDescriptor device, CancellationToken ct) => Task.Run(() =>
    {
        ct.ThrowIfCancellationRequested();
        if (!DevicePolicy.Visible(device)) return new BatteryReading(device, null, null, Availability.Unsupported,
            DateTimeOffset.UtcNow, "Wireless neconfirmat; nu se trimite comanda POWER");
        if (!_devices.TryGetValue(device.Id, out var endpoint)) return BatteryReading.Unavailable(device);
        if (!endpoint.Device.TryOpen(out var stream)) return BatteryReading.Unavailable(device, "Dispozitivul este ocupat");
        using (stream)
        using (ct.Register(stream.Dispose))
        {
            stream.ReadTimeout = 700;
            stream.WriteTimeout = 1800;
            var request = new byte[endpoint.OutputLength];
            PulsarProtocol.PowerRequest().CopyTo(request, 0);
            // SET_REPORT is the Nordic control route. GET_REPORT also wakes
            // receivers which otherwise leave the interrupt query unanswered.
            // Keep interrupt input too: the tested Xlite V3 answers there.
            var input = new byte[endpoint.InputLength];
            for (var attempt = 0; attempt < 3; attempt++)
            {
                ct.ThrowIfCancellationRequested();
                HidInventory.SendOutputControl(endpoint, request);
                ct.WaitHandle.WaitOne(150);
                ct.ThrowIfCancellationRequested();
                try
                {
                    var control = HidInventory.ReadInputControl(endpoint, 0x08);
                    if (PulsarProtocol.TryParse(control, out var p, out var c))
                        return new BatteryReading(device, p, c, Availability.Available, DateTimeOffset.UtcNow, c ? "Se încarcă" : "Conectat");
                }
                catch (System.ComponentModel.Win32Exception) { /* Optional GET_REPORT unsupported by firmware. */ }
                var watch = Stopwatch.StartNew();
                while (watch.Elapsed < TimeSpan.FromMilliseconds(800))
                {
                    ct.ThrowIfCancellationRequested();
                    int count;
                    try { count = stream.Read(input); }
                    catch (TimeoutException) { break; }
                    if (PulsarProtocol.TryParse(input.AsSpan(0, count), out var percent, out var charging))
                        return new BatteryReading(device, percent, charging, Availability.Available,
                            DateTimeOffset.UtcNow, charging ? "Se încarcă" : "Conectat");
                }
            }
            return BatteryReading.Unavailable(device, "Receiver prezent; mouse oprit sau răspuns invalid");
        }
    }, ct);
}
