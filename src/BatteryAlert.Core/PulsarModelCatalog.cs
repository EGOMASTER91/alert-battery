namespace BatteryAlert.Core;

public static class PulsarModelCatalog
{
    // Explicit Nordic mouse IDs declared by Pulsar's public cMouse driver configuration.
    // Snapshot: https://bbb.pulsar.gg/cMouse/cfg.json (version 1.3.12, 2026-10-10).
    // cMouse sends report 0x08, command 0x04 for battery. It also covers Xlite CrazyLight.
    // Never apply this protocol to all devices sharing a vendor ID.
    private static readonly HashSet<int> BibimbapMouseIds =
    [
        0x5406, 0x5407, 0x5501, 0x5502, 0x5504, 0x5601, 0x5603,
        0x3414, 0x3415, 0x7501, 0x7502, 0x7503, 0x7504, 0x7505, 0x7506, 0x7507, 0x7508, 0x7509, 0x7510,
        0x3506, 0x3507, 0x3508, 0x3509, 0x3510, 0x3512, 0x3513, 0x3514, 0x3515, 0x3516, 0x3517, 0x3518,
        0x3519, 0x3524, 0x3525, 0x3526, 0x3527, 0x3528, 0x3529,
        0x3601, 0x3603, 0x3604, 0x3606, 0x3607, 0x3608, 0x3609, 0x3610, 0x3611,
        0x9502, 0x9503, 0x9601, 0x7601, 0x7602, 0x7603, 0x7604, 0x7605
    ];

    public static bool Fusion(int vendor, int product) =>
        (vendor == 0x3554 && product is 0xf507 or 0xf508)
        || (vendor == 0x25a7 && product is 0xfa7b or 0xfa7c);
    public static bool CrazyLight(int vendor, int product) =>
        (vendor == 0x3710 && BibimbapMouseIds.Contains(product))
        || (vendor == 0x3554 && product == 0xf517);
    public static bool Supported(int vendor, int product) => Fusion(vendor, product) || CrazyLight(vendor, product);

    public static bool IsReceiver(int vendor, int product) => Fusion(vendor, product)
        || vendor == 0x3554 && product == 0xF517
        || vendor == 0x3710 && product is 0x5406 or 0x5407 or 0x5501 or 0x5502 or 0x5504 or 0x5601 or 0x5603;

    public static bool IsKeyboardReceiver(int vendor, int product) => vendor is 0x3554 or 0x3710 && product == 0xF809;

    public static DeviceDescriptor Classify(DeviceDescriptor device, int vendor, int product)
    {
        var receiver = IsReceiver(vendor, product);
        // Cable PIDs describe a connection, not a guarantee of radio capability.
        // CrazyLight mice are wireless models; other ambiguous cable models
        // remain unconfirmed instead of being guessed from the vendor alone.
        var knownWirelessModel = Supported(vendor, product) &&
            (device.Name.Contains("CrazyLight", StringComparison.OrdinalIgnoreCase)
            || device.Name.Contains("Crazy Light", StringComparison.OrdinalIgnoreCase)
            || device.Name.Contains("Xlite V3", StringComparison.OrdinalIgnoreCase)
            || device.Name.Contains("Xlite V4", StringComparison.OrdinalIgnoreCase)
            || System.Text.RegularExpressions.Regex.IsMatch(device.Name, @"\bX(?:2(?:A|H)?|3)\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase));
        return device with { Kind = DeviceKind.Mouse,
            Wireless = receiver || knownWirelessModel ? WirelessCapability.Confirmed : WirelessCapability.Unknown,
            Connection = receiver ? ConnectionKind.UsbReceiver : ConnectionKind.UsbCable,
            Evidence = receiver ? "Receiver Pulsar documentat în catalog" : knownWirelessModel
                ? "Model Pulsar wireless identificat; conexiune prin cablu" : "PID de conexiune prin cablu; capacitatea wireless necesită identificarea modelului" };
    }
}
