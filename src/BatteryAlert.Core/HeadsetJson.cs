using System.Text.Json;

namespace BatteryAlert.Core;

public static class HeadsetJson
{
    public static IReadOnlyList<BatteryReading> Parse(string json, DateTimeOffset now)
    {
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("devices", out var devices) || devices.ValueKind != JsonValueKind.Array)
            throw new JsonException("HeadsetControl: câmpul devices lipsește.");
        var readings = new List<BatteryReading>();
        var occurrences = new Dictionary<string, int>();
        foreach (var item in devices.EnumerateArray())
        {
            var hardware = Text(item, "id_vendor") + ":" + Text(item, "id_product");
            if (hardware == ":") throw new JsonException("HeadsetControl: identificator lipsă.");
            var index = occurrences.GetValueOrDefault(hardware);
            occurrences[hardware] = index + 1;
            var descriptor = new DeviceDescriptor($"headset:{hardware}:{index}",
                Text(item, "device") ?? "Căști USB", "HeadsetControl", hardware);
            int? level = null;
            bool? charging = null;
            var availability = Availability.Unsupported;
            var detail = "Citirea bateriei nu este suportată";
            if (item.TryGetProperty("battery", out var battery))
            {
                var status = Text(battery, "status");
                availability = Availability.Unavailable;
                detail = "Dispozitiv oprit sau baterie indisponibilă";
                if (status is "BATTERY_AVAILABLE" or "BATTERY_CHARGING")
                {
                    charging = status == "BATTERY_CHARGING";
                    if (battery.TryGetProperty("level", out var value) && value.TryGetInt32(out var n) && n is >= 0 and <= 100)
                    {
                        level = n;
                        availability = Availability.Available;
                        detail = charging == true ? "Se încarcă" : "Conectat";
                    }
                }
            }
            descriptor = HeadsetCatalog.Classify(descriptor);
            readings.Add(new(descriptor, level, charging, availability, now, detail));
        }
        return readings;
    }

    private static string? Text(JsonElement item, string name) =>
        item.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
