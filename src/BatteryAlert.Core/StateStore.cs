using System.Text.Json;

namespace BatteryAlert.Core;

public sealed class StateStore(string path)
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    public string Path { get; } = path;
    public string? RecoveryMessage { get; private set; }

    public SavedState Load()
    {
        if (!File.Exists(Path)) return new();
        try
        {
            return Parse(File.ReadAllText(Path));
        }
        catch (Exception ex) when (ex is JsonException or InvalidDataException)
        {
            var backup = Path + ".bak";
            if (File.Exists(backup))
            {
                try
                {
                    var state = Parse(File.ReadAllText(backup));
                    RecoveryMessage = "Configurația a fost recuperată din copia de rezervă.";
                    return state;
                }
                catch (Exception backupException) when (backupException is JsonException or InvalidDataException) { }
            }
            // Keep the corrupt file as evidence; do not silently overwrite it.
            File.Move(Path, Path + ".corrupt-" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            RecoveryMessage = "Configurația deteriorată a fost păstrată; s-au încărcat setările implicite.";
            return new();
        }
    }

    private static SavedState Parse(string json)
    {
        var state = JsonSerializer.Deserialize<SavedState>(json, Options)
            ?? throw new InvalidDataException("Empty state.");
        if (state.Version != 1) throw new InvalidDataException("Unsupported state version.");
        state.Settings ??= new();
        state.Settings.Devices ??= [];
        state.Alerts ??= [];
        state.Pending ??= [];
        if (state.Settings.Devices.Values.Any(value => value == null)
            || state.Alerts.Values.Any(value => value == null)
            || state.Pending.Any(value => value == null || value.Alert == null || string.IsNullOrWhiteSpace(value.Alert.DeviceId)))
            throw new InvalidDataException("Invalid state entries.");
        foreach (var device in state.Settings.Devices.Values) device.Threshold = Math.Clamp(device.Threshold, 1, 95);
        if (!NtfyClient.IsValidServer(state.Settings.NtfyServer)) state.Settings.NtfyServer = "https://ntfy.sh";
        if (!NtfyClient.IsValidTopic(state.Settings.NtfyTopic)) state.Settings.NtfyTopic = "battery-" + Guid.NewGuid().ToString("N");
        return state;
    }

    public void Save(SavedState state)
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(Path))!);
        var temp = Path + ".tmp";
        using (var file = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            JsonSerializer.Serialize(file, state, Options);
            file.Flush(true);
        }
        if (File.Exists(Path)) File.Replace(temp, Path, Path + ".bak");
        else File.Move(temp, Path);
    }
}
