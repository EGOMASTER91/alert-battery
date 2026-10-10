namespace BatteryAlert.Core;

public static class HidReportMap
{
    public static DeviceKind Kind(ReadOnlySpan<byte> bytes)
    {
        var usagePage = 0; var usage = 0; var depth = 0; var found = DeviceKind.Unknown;
        var globals = new Stack<int>();
        for (var i = 0; i < bytes.Length;)
        {
            var prefix = bytes[i++];
            if (prefix == 0xFE) { if (i + 2 > bytes.Length) return DeviceKind.Unknown; var length = bytes[i]; i += 2 + length; if (i > bytes.Length) return DeviceKind.Unknown; continue; }
            var size = (prefix & 3) == 3 ? 4 : prefix & 3;
            if (i + size > bytes.Length) return DeviceKind.Unknown;
            var value = 0; for (var b = 0; b < size; b++) value |= bytes[i++] << (b * 8);
            var type = (prefix >> 2) & 3; var tag = prefix >> 4;
            if (type == 1 && tag == 0) usagePage = value;
            if (type == 1 && tag == 10) globals.Push(usagePage);
            if (type == 1 && tag == 11) { if (globals.Count == 0) return DeviceKind.Unknown; usagePage = globals.Pop(); }
            if (type == 2 && tag == 0) usage = value;
            if (type != 0) continue;
            if (tag == 10)
            {
                if (depth++ == 0 && value == 1)
                {
                    var page = usage >> 16 != 0 ? usage >> 16 : usagePage;
                    var kind = page == 1 ? (usage & 0xFFFF) switch { 2 => DeviceKind.Mouse, 6 => DeviceKind.Keyboard, 4 or 5 => DeviceKind.Other, _ => DeviceKind.Unknown } : DeviceKind.Unknown;
                    if (kind != DeviceKind.Unknown) { if (found != DeviceKind.Unknown && found != kind) return DeviceKind.Unknown; found = kind; }
                }
            }
            if (tag == 12 && --depth < 0) return DeviceKind.Unknown;
            usage = 0;
        }
        return depth == 0 ? found : DeviceKind.Unknown;
    }
}
