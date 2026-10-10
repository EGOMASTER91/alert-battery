namespace BatteryAlert.Core;

// Only POWER (0x04) and connection status (0x03) queries. No settings/firmware writes.
// Wire format documented by andrewrabert/python-pulsar-mouse-tool (MIT).
public static class PulsarProtocol
{
    public static byte[] PowerRequest()
    {
        var report = new byte[17];
        report[0] = 0x08;
        report[1] = 0x04;
        report[16] = Checksum(report.AsSpan(0, 16));
        return report;
    }

    public static byte[] ConnectionRequest()
    {
        var report = PowerRequest();
        report[1] = 0x03;
        report[16] = Checksum(report.AsSpan(0, 16));
        return report;
    }

    public static bool TryParseConnection(ReadOnlySpan<byte> data, out bool online)
    {
        online = false;
        if (!IsReply(data, 0x03) || data[6] > 1) return false;
        online = data[6] == 1;
        return true;
    }

    private static bool IsReply(ReadOnlySpan<byte> data, byte command) =>
        data.Length >= 17 && data[0] == 0x08 && data[1] == command && data[2] == 0
        && data[16] == Checksum(data[..16]);

    public static byte Checksum(ReadOnlySpan<byte> bytes)
    {
        var sum = 0;
        foreach (var b in bytes) sum += b;
        return unchecked((byte)(0x55 - sum));
    }

    public static bool TryParse(ReadOnlySpan<byte> data, out int percent, out bool charging)
    {
        percent = 0;
        charging = false;
        if (!IsReply(data, 0x04) || data[6] > 100 || data[7] > 1) return false;
        // Echoes and sleeping receivers can return a valid checksum with no power data.
        var millivolts = (data[8] << 8) | data[9];
        if (millivolts is < 2000 or > 5500) return false;
        percent = data[6];
        charging = data[7] == 1;
        return true;
    }
}
