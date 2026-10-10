namespace BatteryAlert.Core;

public sealed record ProductIdentity(string Brand, string Category, string? ImageFile)
{
    public static ProductIdentity Resolve(DeviceDescriptor device)
    {
        // Receiver IDs may be shared by several mice. Never infer an image
        // from a shared Pulsar PID: match the model name reported by the device.
        var name = device.Name.ToLowerInvariant().Replace("g pro", "pro");
        if (device.Kind == DeviceKind.Keyboard) return new(name.Contains("logitech") ? "LOGITECH" : name.Contains("pulsar") ? "PULSAR" : "TASTATURĂ", "Tastatură wireless", null);
        if (name.Contains("logitech") || device.Provider == "HeadsetControl" && name.Contains("pro x"))
        {
            if (name.Contains("superlight")) return new("LOGITECH G", "Mouse wireless", null);
            if (name.Contains("pro x 2") || name.Contains("pro x2")) return new("LOGITECH G", "Căști wireless", "logitech-pro-x2.png");
            if (name.Contains("pro x") && (name.Contains("wireless") || name.Contains("lightspeed")))
                return new("LOGITECH G", "Căști wireless", "logitech-pro-x.png");
            return new("LOGITECH G", device.Provider == "HeadsetControl" ? "Căști" : "Dispozitiv wireless", null);
        }
        if (name.Contains("pulsar") || device.Provider.StartsWith("Pulsar", StringComparison.Ordinal))
        {
            if (name.Contains('/') || name.Contains("xlite v3 es")) return new("PULSAR", "Mouse wireless", null);
            if (name.Contains("xlite") && (name.Contains("crazylight") || name.Contains("crazy light")))
                return new("PULSAR", "Mouse wireless", "pulsar-xlite-crazylight.png");
            if (name.Contains("xlite v3") || name.Contains("xlite v 3")) return new("PULSAR", "Mouse wireless", "pulsar-xlite-v3.png");
            return new("PULSAR", "Mouse wireless", null);
        }
        if ((name.Contains("powerbeats pro") || name.Contains("powerbeat pro")) && !name.Contains("pro 2"))
            return new("BEATS", "Căști wireless", "beats-powerbeats-pro.jpg");
        return new("DISPOZITIV", device.Provider == "HeadsetControl" ? "Căști wireless" : "Dispozitiv wireless", null);
    }
}
