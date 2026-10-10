using System.Diagnostics;
using System.Text;
using HidSharp;

namespace BatteryAlert.App.Providers;

public sealed class LogitechHidppProvider : IBatteryProvider
{
    private sealed record Target(HidEndpoint Endpoint, byte Slot);
    private readonly Dictionary<string, Target> _targets = [];
    private readonly Dictionary<string, DeviceDescriptor> _profiles = [];
    public string Name => "Logitech HID++";

    public async Task<IReadOnlyList<DeviceDescriptor>> DiscoverAsync(CancellationToken ct)
    {
        var nodes = await WindowsInventory.SnapshotAsync(ct);
        return await Task.Run<IReadOnlyList<DeviceDescriptor>>(() =>
        {
            var result = new List<DeviceDescriptor>(); _targets.Clear();
            var endpoints = HidInventory.Enumerate().Where(e => e.Device.VendorID == 0x046D
                && e.UsagePage == 0xFF00 && e.Usage is 1 or 2 && e.InputLength >= 20 && e.OutputLength >= 20)
                .GroupBy(e => WindowsInventory.Physical(WindowsInventory.ForHid(nodes, e)) ?? e.Device.DevicePath)
                .Select(g => g.OrderBy(e => e.OutputLength).First());
            foreach (var endpoint in endpoints)
            {
                ct.ThrowIfCancellationRequested();
                // Require the documented HID++ long-report IDs as well as
                // Logitech's vendor collection before sending even a ping.
                try
                {
                    var reports = endpoint.Device.GetReportDescriptor();
                    if (!reports.InputReports.Any(r => r.ReportID == 0x11) || !reports.OutputReports.Any(r => r.ReportID == 0x11)) continue;
                }
                catch (Exception ex) when (ex is IOException or NotSupportedException or UnauthorizedAccessException) { continue; }
                var receiver = HidppProtocol.IsReceiver(endpoint.Device.VendorID, endpoint.Device.ProductID);
                var node = WindowsInventory.ForHid(nodes, endpoint);
                var endpointKey = WindowsInventory.Physical(node) ?? endpoint.Identity;
                if (!endpoint.Device.TryOpen(out var stream)) continue;
                using (stream)
                using (ct.Register(stream.Dispose))
                {
                    stream.ReadTimeout = 180; stream.WriteTimeout = 350;
                    var slots = receiver ? Enumerable.Range(1, 6).Select(n => (byte)n) : new byte[] { 0xFF };
                    foreach (var slot in slots)
                    {
                        var key = endpointKey + ":" + slot;
                        try
                        {
                            var ping = Query(stream, endpoint, slot, 0, 0x10, ct, 0, 0, 0xA5);
                            if (ping == null || ping.Length < 3 || ping[0] < 2 || ping[2] != 0xA5)
                            {
                                if (_profiles.TryGetValue(key, out var sleeping)) { _targets[sleeping.Id] = new(endpoint, slot); result.Add(sleeping); }
                                continue;
                            }
                            DeviceDescriptor descriptor;
                            // Read identity again: replacing a paired peripheral
                            // in the same receiver slot must not reuse its name,
                            // category, battery settings or alert state.
                            {
                                var nameFeature = Feature(stream, endpoint, slot, 0x0005, ct);
                                var kindReply = nameFeature == 0 ? null : Query(stream, endpoint, slot, nameFeature, 0x20, ct);
                                var kind = kindReply is { Length: > 0 } ? HidppProtocol.Kind(kindReply[0]) : DeviceKind.Unknown;
                                var identityFeature = Feature(stream, endpoint, slot, 0x0003, ct);
                                var identity = identityFeature == 0 ? null : Query(stream, endpoint, slot, identityFeature, 0, ct);
                                var radio = receiver || identity is { Length: >= 7 } && (identity[6] & 7) != 0;
                                var unit = identity is { Length: >= 5 } && identity.AsSpan(1, 4).ToArray().Any(b => b != 0)
                                    && identity.AsSpan(1, 4).ToArray().Any(b => b != 0xFF)
                                    ? Convert.ToHexString(identity.AsSpan(1, 4)) : null;
                                var physical = unit != null ? "logitech-unit:" + unit : receiver ? "logitech-slot:" + key : WindowsInventory.Physical(node);
                                var id = "hidpp:" + (physical ?? key);
                                var name = nameFeature == 0 ? null : ReadName(stream, endpoint, slot, nameFeature, ct);
                                descriptor = new(id, name == null ? "Logitech · periferic " + slot : "Logitech " + name, Name,
                                    endpoint.HardwareId, endpoint.Device.DevicePath, kind,
                                    radio ? WirelessCapability.Confirmed : identity is { Length: >= 7 }
                                        ? WirelessCapability.WiredOnly : WirelessCapability.Unknown,
                                    receiver ? ConnectionKind.UsbReceiver : ConnectionKind.UsbCable,
                                    receiver ? "Receiver Logitech din catalog; categoria citită prin HID++"
                                        : "HID++ DeviceInfo: transporturi acceptate de dispozitiv", physical);
                                _profiles[key] = descriptor;
                            }
                            _targets[descriptor.Id] = new(endpoint, slot); result.Add(descriptor);
                        }
                        catch (Exception ex) when (!ct.IsCancellationRequested && ex is IOException or TimeoutException or UnauthorizedAccessException)
                        { /* A sleeping slot or unsupported protocol must not stop other slots. */ }
                    }
                }
            }
            return result.DistinctBy(d => d.Id).ToArray();
        }, ct);
    }

    public Task<BatteryReading> ReadAsync(DeviceDescriptor device, CancellationToken ct) => Task.Run(() =>
    {
        if (!DevicePolicy.Visible(device)) return new BatteryReading(device, null, null, Availability.Unsupported, DateTimeOffset.UtcNow, DevicePolicy.Exclusion(device));
        if (!_targets.TryGetValue(device.Id, out var target) || !target.Endpoint.Device.TryOpen(out var stream)) return BatteryReading.Unavailable(device);
        using (stream)
        using (ct.Register(stream.Dispose))
        {
            stream.ReadTimeout = 180; stream.WriteTimeout = 350;
            var ping = Query(stream, target.Endpoint, target.Slot, 0, 0x10, ct, 0, 0, 0xA5);
            if (ping == null) return BatteryReading.Unavailable(device, "Periferic oprit sau receiver fără răspuns");
            var unified = Feature(stream, target.Endpoint, target.Slot, 0x1004, ct);
            var legacy = unified == 0 ? Feature(stream, target.Endpoint, target.Slot, 0x1000, ct) : (byte)0;
            if (unified == 0 && legacy == 0) return new BatteryReading(device, null, null, Availability.Unsupported, DateTimeOffset.UtcNow, "Citirea bateriei nu este suportată · HID++ fără procent");
            var percentage = true;
            if (unified != 0)
            {
                var caps = Query(stream, target.Endpoint, target.Slot, unified, 0, ct);
                if (caps == null) return BatteryReading.Unavailable(device, "Capabilitățile bateriei nu au răspuns");
                percentage = caps.Length > 1 && (caps[1] & 2) != 0;
            }
            var payload = Query(stream, target.Endpoint, target.Slot, unified != 0 ? unified : legacy, unified != 0 ? (byte)0x10 : (byte)0, ct);
            if (payload == null || !HidppProtocol.TryBattery(unified != 0 ? (ushort)0x1004 : (ushort)0x1000, payload, percentage, out var percent, out var charging))
                return BatteryReading.Unavailable(device, "Răspuns HID++ lipsă sau invalid");
            return new BatteryReading(device, percent, charging, percent.HasValue ? Availability.Available : Availability.Unsupported,
                DateTimeOffset.UtcNow, percent.HasValue ? "Baterie citită prin HID++" : "Nivel aproximativ; procentul bateriei nu este oferit");
        }
    }, ct);

    private static byte Feature(HidStream stream, HidEndpoint endpoint, byte slot, ushort feature, CancellationToken ct)
    { var response = Query(stream, endpoint, slot, 0, 0, ct, (byte)(feature >> 8), (byte)feature, 0);
        if (response == null) throw new TimeoutException("HID++: interogarea funcției nu a răspuns."); return response[0]; }
    private static string? ReadName(HidStream stream, HidEndpoint endpoint, byte slot, byte feature, CancellationToken ct)
    {
        var length = Query(stream, endpoint, slot, feature, 0, ct);
        if (length is not { Length: > 0 } || length[0] is 0 or > 80) return null;
        var name = new List<byte>();
        while (name.Count < length[0])
        {
            var fragment = Query(stream, endpoint, slot, feature, 0x10, ct, (byte)name.Count);
            if (fragment == null || fragment.Length == 0) return null;
            name.AddRange(fragment.Take(Math.Min(fragment.Length, length[0] - name.Count)));
        }
        return Encoding.UTF8.GetString(name.ToArray()).TrimEnd('\0');
    }
    private static byte[]? Query(HidStream stream, HidEndpoint endpoint, byte slot, byte feature, byte function, CancellationToken ct, params byte[] data)
    {
        ct.ThrowIfCancellationRequested();
        var request = HidppProtocol.Request(slot, feature, function, data);
        var padded = new byte[endpoint.OutputLength]; request.CopyTo(padded, 0); stream.Write(padded);
        var watch = Stopwatch.StartNew(); var input = new byte[endpoint.InputLength];
        while (watch.ElapsedMilliseconds < 250)
        {
            ct.ThrowIfCancellationRequested();
            int count; try { count = stream.Read(input); } catch (TimeoutException) { return null; }
            var response = input.AsSpan(0, count);
            if (HidppProtocol.IsError(response, request)) return null;
            if (HidppProtocol.Matches(response, request)) return response.Slice(4, response[0] == 0x10 ? Math.Min(3, count - 4) : Math.Min(16, count - 4)).ToArray();
        }
        return null;
    }
}
