namespace BatteryAlert.Core;

// Model facts from the bundled HeadsetControl 4.1.0 source/support table.
// Mixed wired/wireless families are resolved by product ID, never by the
// presence of a battery field or a low/zero response from an audio DAC.
public static class HeadsetCatalog
{
    private static readonly HashSet<string> WirelessNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Logitech ASTRO A50 Gen 5", "Logitech G522 LIGHTSPEED", "Logitech G533", "Logitech G535", "Logitech G930",
        "Corsair Wireless V2 Headset Device", "Corsair Virtuoso XT/SE",
        "SteelSeries Arctis (1/7X/7P) Wireless", "SteelSeries Arctis 9", "SteelSeries Arctis Pro Wireless",
        "SteelSeries Arctis Nova (5/5X)", "SteelSeries Arctis Nova 7", "SteelSeries Arctis Nova 7P",
        "SteelSeries Arctis 7+", "SteelSeries Arctis Nova Pro Wireless", "SteelSeries Arctis Nova 3P Wireless",
        "SteelSeries Arctis GameBuds", "HyperX Cloud Alpha Wireless", "HyperX Cloud Flight Wireless",
        "HyperX Cloud II Wireless", "HyperX Cloud II Wireless (Kingston)", "ROCCAT Elo 7.1 Air",
        "Audeze Maxwell", "Audeze Maxwell 2", "Lenovo Wireless VoIP Headset",
        "Plantronics Voyager 8200 UC (BT600)", "Sony INZONE Buds", "Sony INZONE H5", "MCHOSE X9"
    };
    private static readonly HashSet<string> WiredNames = new(StringComparer.OrdinalIgnoreCase)
    { "Logitech G431/G432/G433", "Logitech Zone Wired/Zone 750", "HyperX Cloud 3", "ROCCAT Elo 7.1 USB", "SteelSeries Arctis Nova 3" };
    public static DeviceDescriptor Classify(DeviceDescriptor descriptor)
    {
        var hardware = descriptor.HardwareId?.ToLowerInvariant();
        var wireless = hardware is "0x046d:0x0af7" or "0x046d:0x0aba" or "0x046d:0x0afb" or "0x046d:0x0afc"
            or "0x046d:0x0a5b" or "0x046d:0x0a87" or "0x046d:0x0ab5" or "0x046d:0x0afe" or "0x046d:0x0b1f"
            or "0x1038:0x1260" or "0x1038:0x12ad" || WirelessNames.Contains(descriptor.Name);
        var wired = hardware is "0x046d:0x0aa7" or "0x046d:0x0aaa" or "0x046d:0x0a5c" or "0x046d:0x0a89"
            or "0x1038:0x1252" or "0x1038:0x1280" || WiredNames.Contains(descriptor.Name);
        var cable = hardware is "0x1b1c:0x0a62" or "0x1b1c:0x0a3d";
        return descriptor with { Kind = DeviceKind.Headset,
            Wireless = wired ? WirelessCapability.WiredOnly : wireless ? WirelessCapability.Confirmed : WirelessCapability.Unknown,
            Connection = wired || cable ? ConnectionKind.UsbCable : wireless ? ConnectionKind.UsbReceiver : ConnectionKind.Unknown,
            Evidence = wired ? "Model cu fir identificat în catalogul HeadsetControl" : wireless
                ? "Model wireless identificat în catalogul HeadsetControl" : "Model USB; capacitatea wireless neconfirmată" };
    }
}
