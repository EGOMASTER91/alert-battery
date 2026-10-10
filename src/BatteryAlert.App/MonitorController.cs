using BatteryAlert.App.Providers;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Net.Http;
using System.Windows.Threading;

namespace BatteryAlert.App;

public sealed class MonitorController : INotifyPropertyChanged, IDisposable
{
    private readonly Dispatcher _dispatcher;
    private readonly StateStore _store;
    private readonly SavedState _state;
    private readonly NotificationService _notifications;
    private readonly HttpClient _http = new(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(10) };
    private readonly NtfyClient _ntfy;
    private readonly IBatteryProvider[] _providers;
    private readonly ProviderPoller _poller = new();
    private readonly SemaphoreSlim _refresh = new(1, 1);
    private readonly CancellationTokenSource _stop = new();
    private readonly string _logPath;
    private string _status = "Pregătit pentru monitorizare";
    private string _lastEvent = "Nicio alertă trimisă";
    private bool _paused;
    private Task? _loop;
    public ObservableCollection<DeviceRow> Devices { get; } = [];
    public ObservableCollection<DeviceRow> Compatibility { get; } = [];
    public System.ComponentModel.ICollectionView VisibleDevices { get; }
    private DeviceKind? _filter;
    public bool IsFilterEmpty => VisibleDevices.IsEmpty;
    public void Filter(DeviceKind? kind) { _filter = kind; VisibleDevices.Refresh(); Changed(nameof(IsFilterEmpty)); }
    public AppSettings Settings => _state.Settings;
    public AppSettings DraftSettings { get; }
    public string Status { get => _status; private set { _status = value; Changed(nameof(Status)); } }
    public string LastEvent { get => _lastEvent; private set { _lastEvent = value; Changed(nameof(LastEvent)); } }
    public bool Paused { get => _paused; private set { _paused = value; Changed(nameof(Paused)); Changed(nameof(MonitorLabel)); Changed(nameof(PauseLabel)); } }
    public string MonitorLabel => Paused ? "În pauză" : "Monitorizare activă";
    public string PauseLabel => Paused ? "Reia" : "Pauză";
    public int DeviceCount => Devices.Count;
    public int LiveCount => Devices.Count(row => row.IsLive);
    public int LowCount => Devices.Count(row => row.Settings.Enabled && row.NeedsAttention);
    public bool IsEmpty => Devices.Count == 0;
    public event PropertyChangedEventHandler? PropertyChanged;

    public MonitorController(Dispatcher dispatcher, NotificationService notifications, string dataFolder)
    {
        _dispatcher = dispatcher;
        VisibleDevices = System.Windows.Data.CollectionViewSource.GetDefaultView(Devices);
        VisibleDevices.Filter = item => item is DeviceRow row && (_filter == null || row.Device.Kind == _filter);
        Directory.CreateDirectory(dataFolder);
        _store = new(System.IO.Path.Combine(dataFolder, "state.json"));
        _state = _store.Load();
        DraftSettings = Settings.Clone();
        _notifications = notifications;
        _ntfy = new(_http);
        _logPath = System.IO.Path.Combine(dataFolder, "diagnostics.log");
        _providers = CreateProviders();
        _poller.Error += Log;
        if (_store.RecoveryMessage != null) LastEvent = _store.RecoveryMessage;
        if (notifications.RegistrationError != null) LastEvent = notifications.RegistrationError;
        _store.Save(_state);
    }

    public static IBatteryProvider[] CreateProviders() =>
    [
        new HeadsetProvider(System.IO.Path.Combine(AppContext.BaseDirectory, "vendor", "headsetcontrol", "headsetcontrol.exe")),
        new PulsarProvider(false), new PulsarProvider(true), new WindowsBatteryProvider(),
        new LogitechHidppProvider(), new CompatibilityProvider()
    ];

    public void Start()
    {
        _loop = RunAsync();
    }

    private async Task RunAsync()
    {
        try
        {
            await RefreshAsync();
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(60));
            while (await timer.WaitForNextTickAsync(_stop.Token)) await RefreshAsync();
        }
        catch (OperationCanceledException) when (_stop.IsCancellationRequested) { }
        catch (Exception ex)
        {
            Log(ex.Message);
            await _dispatcher.InvokeAsync(() => Status = "Monitorizarea s-a oprit: " + ex.Message);
        }
    }

    public async Task RefreshAsync()
    {
        if (_stop.IsCancellationRequested || !await _refresh.WaitAsync(0)) return;
        try
        {
            if (Paused) return;
            await _dispatcher.InvokeAsync(() => Status = "Se citesc dispozitivele…");
            var readings = await _poller.PollAsync(_providers, _stop.Token);
            await _dispatcher.InvokeAsync(() => { if (!Paused) Apply(readings); });
            if (!Paused) await FlushPhoneAsync();
            await _dispatcher.InvokeAsync(() =>
            { if (!Paused) Status = $"Monitorizare activă · verificat la {DateTime.Now:HH:mm:ss} · interval 60 s"; });
        }
        catch (OperationCanceledException) when (_stop.IsCancellationRequested) { }
        catch (Exception ex)
        {
            Log(ex.Message);
            await _dispatcher.InvokeAsync(() => Status = "Eroare de monitorizare: " + ex.Message);
        }
        finally { _refresh.Release(); }
    }

    private void Apply(IReadOnlyList<BatteryReading> readings)
    {
        var now = DateTimeOffset.UtcNow;
        var seen = new HashSet<string>();
        var alerts = new List<(LowBatteryAlert Alert, DeviceSettings Settings)>();
        var merged = DevicePolicy.Merge(readings);
        Compatibility.Clear();
        foreach (var reading in merged)
        {
            var compatible = new DeviceRow(reading.Device, new() { Enabled = false }); compatible.Update(reading);
            Compatibility.Add(compatible);
        }
        // Remove rows reclassified as wired/other, and old aliases replaced by
        // a positive physical identity. Keep disconnected wireless rows.
        var hidden = merged.Where(r => !DevicePolicy.Visible(r.Device)).Select(r => r.Device.Id).ToHashSet();
        var aliases = merged.SelectMany(r => r.Device.LegacyIds ?? []).ToHashSet();
        foreach (var row in Devices.Where(r => hidden.Contains(r.Device.Id) || aliases.Contains(r.Device.Id)).ToArray()) Devices.Remove(row);
        foreach (var reading in merged.Where(r => DevicePolicy.Visible(r.Device)))
        {
            DevicePolicy.PreserveState(_state, reading.Device);
            if (!DraftSettings.Devices.ContainsKey(reading.Device.Id) && Settings.Devices.TryGetValue(reading.Device.Id, out var migrated))
                DraftSettings.Devices[reading.Device.Id] = migrated.Clone();
            if (!seen.Add(reading.Device.Id)) continue;
            var row = Devices.FirstOrDefault(r => r.Device.Id == reading.Device.Id);
            if (row == null)
            {
                Settings.For(reading.Device.Id);
                row = new(reading.Device, DraftSettings.For(reading.Device.Id)); Devices.Add(row);
                row.PropertyChanged += (_, _) => NotifySummary();
            }
            row.Update(reading);
            if (!_state.Alerts.TryGetValue(reading.Device.Id, out var state))
                _state.Alerts[reading.Device.Id] = state = new();
            var active = Settings.For(reading.Device.Id);
            var effective = new DeviceSettings
            {
                Enabled = active.Enabled, Threshold = active.Threshold,
                Desktop = active.Desktop, Phone = active.Phone && Settings.PhoneEnabled, Sound = active.Sound
            };
            var alert = AlertEngine.Evaluate(reading, effective, state, now);
            if (alert == null) continue;
            if (effective.Phone) _state.Pending.Add(new() { Alert = alert, NextAttempt = now });
            alerts.Add((alert, effective));
        }
        foreach (var row in Devices.Where(r => !seen.Contains(r.Device.Id)))
            row.Update(BatteryReading.Unavailable(row.Device, "Dispozitiv deconectat"));
        VisibleDevices.Refresh();
        // Persist the latch and phone outbox before delivery, to avoid repeating after restart.
        NotifySummary();
        _store.Save(_state);
        foreach (var (alert, settings) in alerts)
        {
            LastEvent = alert.Message;
            if (!settings.Desktop) continue;
            try { _notifications.Show(alert.Message, Settings.Sound && settings.Sound); }
            catch (Exception ex)
            {
                Log(ex.Message);
                LastEvent = "Alerta Windows a eșuat; verifică setările de notificări.";
                if (Settings.Sound && settings.Sound) NotificationService.PlaySound();
            }
        }
    }

    private async Task FlushPhoneAsync()
    {
        var pending = await _dispatcher.InvokeAsync(() => _state.Pending.ToArray());
        foreach (var item in pending)
        {
            var shouldSend = await _dispatcher.InvokeAsync(() =>
            {
                var row = Devices.FirstOrDefault(r => r.Device.Id == item.Alert.DeviceId);
                var relevant = Settings.PhoneEnabled && NotificationQueue.Relevant(item, row?.Latest,
                    Settings.For(item.Alert.DeviceId), DateTimeOffset.UtcNow);
                if (!relevant) { _state.Pending.Remove(item); _store.Save(_state); return false; }
                return item.NextAttempt <= DateTimeOffset.UtcNow;
            });
            if (!shouldSend) continue;
            var destination = await _dispatcher.InvokeAsync(() => new AppSettings
            { NtfyServer = Settings.NtfyServer, NtfyTopic = Settings.NtfyTopic });
            try
            {
                await _ntfy.SendAsync(destination, item.Alert.Message, _stop.Token);
                await _dispatcher.InvokeAsync(() => { _state.Pending.Remove(item); _store.Save(_state); });
            }
            catch (Exception ex) when (!_stop.IsCancellationRequested)
            {
                // Do not log endpoints/topics or credentials.
                Log("Livrarea ntfy a eșuat: " + ex.GetType().Name);
                await _dispatcher.InvokeAsync(() =>
                {
                    if (ex is NtfyDeliveryException { Permanent: true }
                        || !NotificationQueue.ScheduleRetry(item, DateTimeOffset.UtcNow, (ex as NtfyDeliveryException)?.RetryAfter))
                        _state.Pending.Remove(item);
                    LastEvent = "Alerta pe telefon nu a fost livrată; alertele locale rămân active.";
                    _store.Save(_state);
                });
            }
        }
    }

    public void SaveSettings()
    {
        if (!NtfyClient.IsValidServer(DraftSettings.NtfyServer) || !NtfyClient.IsValidTopic(DraftSettings.NtfyTopic))
            throw new ArgumentException("Folosește un server HTTPS și un canal de 1–64 caractere: litere, cifre, - sau _.");
        foreach (var settings in DraftSettings.Devices.Values)
            if (settings.Threshold is < 1 or > 95) throw new ArgumentException("Pragul trebuie să fie între 1% și 95%.");
        StartupRegistration.Apply(DraftSettings.StartWithWindows);
        _state.Settings = DraftSettings.Clone();
        _store.Save(_state);
        LastEvent = "Setările au fost salvate.";
    }

    public void TogglePause()
    {
        Paused = !Paused;
        Status = Paused ? "Monitorizare în pauză" : "Monitorizare reluată";
        if (!Paused) _ = RefreshAsync();
    }

    public void TestDesktop() => _notifications.Show("Dispozitiv de test — baterie 10%", DraftSettings.Sound);
    public Task TestPhoneAsync() => _ntfy.SendAsync(DraftSettings, "Battery Alert — notificare de test", _stop.Token);

    private readonly object _logLock = new();
    private void Log(string message)
    {
        lock (_logLock)
        {
            try
            {
                if (File.Exists(_logPath) && new FileInfo(_logPath).Length > 1_000_000)
                    File.Move(_logPath, _logPath + ".old", true);
                File.AppendAllText(_logPath, $"{DateTimeOffset.Now:O} {message}{Environment.NewLine}");
            }
            catch (IOException) { }
        }
    }

    private void Changed(string name) => PropertyChanged?.Invoke(this, new(name));
    private void NotifySummary()
    { Changed(nameof(DeviceCount)); Changed(nameof(LiveCount)); Changed(nameof(LowCount)); Changed(nameof(IsEmpty)); Changed(nameof(IsFilterEmpty)); }
    public void Dispose()
    {
        _stop.Cancel();
        _http.Dispose();
        // Cancellation source/semaphore remain alive until in-flight tasks unwind.
    }
}

