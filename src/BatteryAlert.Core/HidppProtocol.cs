namespace BatteryAlert.Core;

// Independent codec for read-only HID++ 2.0 requests. Software ID 0xA
// separates replies from notifications and other clients such as G HUB.
public static class HidppProtocol
{
    public static bool IsReceiver(int vendor, int product) => vendor == 0x046D && product is
        0xC52B or 0xC532 or 0xC548 or 0xC52F or 0xC531 or 0xC534 or 0xC537
        or 0xC539 or 0xC53A or 0xC53D or 0xC53F or 0xC541 or 0xC545 or 0xC547 or 0xC54D;
    public static byte[] Request(byte slot, byte feature, byte function, params byte[] data)
    {
        if (data.Length > 16 || (function & 15) != 0) throw new ArgumentException("Invalid HID++ request.");
        var report = new byte[20]; report[0] = 0x11; report[1] = slot; report[2] = feature; report[3] = (byte)(function | 0x0A);
        data.CopyTo(report, 4); return report;
    }
    public static bool Matches(ReadOnlySpan<byte> response, ReadOnlySpan<byte> request) => request.Length >= 4 && response.Length >= 7
        && (response[0] == 0x10 || response[0] == 0x11 && response.Length >= 20)
        && response[1] == request[1] && response[2] == request[2] && response[3] == request[3];
    public static bool IsError(ReadOnlySpan<byte> response, ReadOnlySpan<byte> request) => request.Length >= 4 && response.Length >= 7
        && response[1] == request[1] && response[2] is 0x8F or 0xFF && response[3] == request[2] && response[4] == request[3];
    public static DeviceKind Kind(byte kind) => kind switch
    { 0 or 2 => DeviceKind.Keyboard, 3 => DeviceKind.Mouse, 1 or 4 or 5 or 6 or 7 => DeviceKind.Other, _ => DeviceKind.Unknown };
    public static bool TryBattery(ushort feature, ReadOnlySpan<byte> payload, bool percentageSupported, out int? percent, out bool? charging)
    {
        percent = null; charging = null;
        if (feature is not (0x1000 or 0x1004) || payload.Length < (feature == 0x1004 ? 4 : 3)) return false;
        if (payload[0] > 100 || payload[2] > 4) return false;
        // Zero means unspecified in legacy battery status. Unified capacity can
        // be zero only when capabilities explicitly advertise percentage support.
        if (percentageSupported && (payload[0] > 0 || feature == 0x1004)) percent = payload[0];
        charging = payload[2] switch { 1 or 2 or 3 or 4 => true, 0 => false, _ => null };
        return true;
    }
}
