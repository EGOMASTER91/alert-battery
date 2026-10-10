using System.Diagnostics;

namespace BatteryAlert.App.Providers;

public sealed class HeadsetProvider(string executable) : IBatteryProvider
{
    private IReadOnlyList<BatteryReading> _readings = [];
    public string Name => "HeadsetControl";

    public async Task<IReadOnlyList<DeviceDescriptor>> DiscoverAsync(CancellationToken ct)
    {
        if (!File.Exists(executable)) throw new FileNotFoundException("Componenta HeadsetControl lipsește.", executable);
        var start = new ProcessStartInfo(executable)
        {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true
        };
        foreach (var argument in new[] { "-b", "-o", "json", "--timeout", "2000" }) start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new IOException("HeadsetControl nu a pornit.");
        using var registration = ct.Register(() =>
        {
            try { if (!process.HasExited) process.Kill(true); }
            catch (InvalidOperationException) { }
        });
        var output = process.StandardOutput.ReadToEndAsync(ct);
        var errors = process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);
        var text = await output;
        var stderr = await errors;
        ct.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(text)) throw new IOException("HeadsetControl nu a returnat date: " + stderr.Trim());
        _readings = HeadsetJson.Parse(text, DateTimeOffset.UtcNow);
        var nodes = await WindowsInventory.SnapshotAsync(ct);
        var endpoints = HidInventory.Enumerate();
        _readings = _readings.Select(r =>
        {
            var hardware = r.Device.HardwareId?.Replace("0x", "", StringComparison.OrdinalIgnoreCase);
            var identities = endpoints.Where(e => string.Equals(e.HardwareId, hardware, StringComparison.OrdinalIgnoreCase))
                .Select(e => WindowsInventory.Physical(WindowsInventory.ForHid(nodes, e))).Where(p => p != null).Distinct().ToArray();
            // Never correlate two identical headsets by enumeration order.
            return identities.Length == 1 && _readings.Count(x => x.Device.HardwareId == r.Device.HardwareId) == 1
                ? r with { Device = r.Device with { PhysicalId = identities[0] } } : r;
        }).ToArray();
        return _readings.Select(r => r.Device).ToArray();
    }

    public Task<BatteryReading> ReadAsync(DeviceDescriptor device, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(_readings.FirstOrDefault(r => r.Device.Id == device.Id) ?? BatteryReading.Unavailable(device));
    }
}
