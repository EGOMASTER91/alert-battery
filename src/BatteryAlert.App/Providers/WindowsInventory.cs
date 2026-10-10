using Windows.Devices.Enumeration;

namespace BatteryAlert.App.Providers;

public sealed record WindowsNode(string Id, string? Container, string Instance, string Class,
    DeviceKind Kind, IReadOnlyDictionary<string, object> Properties);

public static class WindowsInventory
{
    private static readonly object Gate = new();
    private static Task<IReadOnlyList<WindowsNode>>? _snapshot;
    private static DateTimeOffset _started;
    public static Task<IReadOnlyList<WindowsNode>> SnapshotAsync(CancellationToken ct)
    {
        Task<IReadOnlyList<WindowsNode>> task;
        lock (Gate)
        {
            if (_snapshot == null || _snapshot.IsCompleted && DateTimeOffset.UtcNow - _started > TimeSpan.FromSeconds(2))
            { _started = DateTimeOffset.UtcNow; _snapshot = LoadAsync(); }
            task = _snapshot;
        }
        return task.WaitAsync(ct);
    }
    private static async Task<IReadOnlyList<WindowsNode>> LoadAsync()
    {
        var devices = await DeviceInformation.FindAllAsync(null, new[]
        {
            "System.Devices.ContainerId", "System.Devices.DeviceInstanceId", "System.Devices.ClassGuid",
            "System.Devices.CompatibleIds", "{104EA319-6EE2-4701-BD47-8DDBF425BBE5} 2"
        }, DeviceInformationKind.Device).AsTask();
        return devices.Select(d =>
        {
            string? Prop(string key) => d.Properties.TryGetValue(key, out var value) ? Convert.ToString(value) : null;
            var cls = Prop("System.Devices.ClassGuid")?.Trim('{', '}').ToLowerInvariant() ?? "";
            var kind = cls switch { "4d36e96b-e325-11ce-bfc1-08002be10318" => DeviceKind.Keyboard,
                "4d36e96f-e325-11ce-bfc1-08002be10318" => DeviceKind.Mouse, _ => DeviceKind.Unknown };
            if (d.Properties.TryGetValue("System.Devices.CompatibleIds", out var raw) && raw is string[] compatible)
            {
                if (compatible.Contains("HID_DEVICE_SYSTEM_KEYBOARD", StringComparer.OrdinalIgnoreCase)) kind = DeviceKind.Keyboard;
                else if (compatible.Contains("HID_DEVICE_SYSTEM_MOUSE", StringComparer.OrdinalIgnoreCase)) kind = DeviceKind.Mouse;
            }
            return new WindowsNode(d.Id, Prop("System.Devices.ContainerId"), Prop("System.Devices.DeviceInstanceId") ?? d.Id,
                cls, kind, d.Properties);
        }).ToArray();
    }
    public static WindowsNode? ForHid(IReadOnlyList<WindowsNode> nodes, HidEndpoint endpoint)
    {
        var pieces = endpoint.Device.DevicePath.Split('#');
        if (pieces.Length < 3) return null;
        var instance = pieces[0].Replace(@"\\?\", "") + "\\" + pieces[1] + "\\" + pieces[2];
        return nodes.FirstOrDefault(n => string.Equals(n.Instance, instance, StringComparison.OrdinalIgnoreCase));
    }
    public static string? Physical(WindowsNode? node) => node?.Container is { Length: > 0 } container
        && Guid.TryParse(container, out var guid) && guid != Guid.Empty ? "container:" + guid.ToString("D") : null;
}
