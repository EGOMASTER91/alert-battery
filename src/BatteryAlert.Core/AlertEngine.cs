namespace BatteryAlert.Core;

public static class AlertEngine
{
    public static LowBatteryAlert? Evaluate(BatteryReading reading, DeviceSettings settings,
        AlertState state, DateTimeOffset now)
    {
        if (!settings.Enabled || !reading.IsValid || now - reading.ReadAt > TimeSpan.FromMinutes(2)
            || reading.ReadAt > now + TimeSpan.FromSeconds(5)) return null;
        var threshold = Math.Clamp(settings.Threshold, 1, 95);
        if (state.Threshold != threshold)
        {
            state.Threshold = threshold;
            state.Alerted = false;
        }
        if (reading.Percent >= threshold + 5)
        {
            state.Alerted = false;
            return null;
        }
        if (reading.Charging == true || reading.Percent > threshold || state.Alerted) return null;
        if (!settings.Desktop && !settings.Phone) return null;
        state.Alerted = true;
        return new(Guid.NewGuid().ToString("N"), reading.Device.Id, reading.Device.Name,
            reading.Percent!.Value, now);
    }
}
