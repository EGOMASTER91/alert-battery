using BatteryAlert.App.Providers;
using Microsoft.Win32;
using System.Diagnostics;
using System.Text.Json;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace BatteryAlert.App;

public partial class App : System.Windows.Application
{
    private Mutex? _mutex;
    private EventWaitHandle? _showEvent;
    private RegisteredWaitHandle? _showWait;
    private Forms.NotifyIcon? _tray;
    private NotificationService? _notifications;
    private MonitorController? _controller;
    private MainWindow? _window;
    private DispatcherTimer? _deviceDebounce;
    private bool _exit;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (e.Args.Length == 2 && e.Args[0] == "--diagnose")
        {
            await DiagnoseAsync(e.Args[1]);
            Shutdown();
            return;
        }
        if (e.Args.Length == 2 && e.Args[0] == "--smoke-test")
        {
            await SmokeTestAsync(e.Args[1]);
            Shutdown();
            return;
        }
        if (e.Args.Length == 2 && (e.Args[0] == "--render-preview" || e.Args[0] == "--render-settings-preview" || e.Args[0] == "--render-small-preview" || e.Args[0] == "--render-compatibility-preview"))
        {
            RenderPreview(e.Args[1], e.Args[0] == "--render-settings-preview", e.Args[0] == "--render-small-preview", e.Args[0] == "--render-compatibility-preview");
            Shutdown();
            return;
        }
        _mutex = new Mutex(true, @"Local\BatteryAlert.Instance", out var created);
        _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\BatteryAlert.Show");
        if (!created) { if (!e.Args.Contains("--background")) _showEvent.Set(); Shutdown(); return; }
        _showWait = ThreadPool.RegisterWaitForSingleObject(_showEvent,
            (_, _) => Dispatcher.BeginInvoke(ShowWindow), null, Timeout.Infinite, false);
        try
        {
            _notifications = new();
            _notifications.OpenRequested += () => Dispatcher.BeginInvoke(ShowWindow);
            _controller = new(Dispatcher, _notifications,
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BatteryAlert"));
            _window = new(_controller);
            _window.SourceInitialized += (_, _) =>
                HwndSource.FromHwnd(new WindowInteropHelper(_window).Handle)?.AddHook(WindowMessage);
            // Create the window handle even when started in the tray, so USB events are received.
            new WindowInteropHelper(_window).EnsureHandle();
            _window.Closing += (_, args) => { if (!_exit) { args.Cancel = true; _window.Hide(); } };
            var menu = new Forms.ContextMenuStrip();
            menu.Items.Add("Deschide Battery Alert", null, (_, _) => ShowWindow());
            var pause = menu.Items.Add("Pauză", null, (_, _) => { _controller.TogglePause(); });
            _controller.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(MonitorController.Paused)) pause.Text = _controller.Paused ? "Reia monitorizarea" : "Pauză";
            };
            menu.Items.Add("Ieșire", null, (_, _) => { _exit = true; Shutdown(); });
            _tray = new() { Icon = MakeTrayIcon(), Text = "Battery Alert · monitorizare baterii", ContextMenuStrip = menu, Visible = true };
            _tray.DoubleClick += (_, _) => ShowWindow();
            _deviceDebounce = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _deviceDebounce.Tick += async (_, _) => { _deviceDebounce.Stop(); await _controller.RefreshAsync(); };
            SystemEvents.PowerModeChanged += PowerChanged;
            DeviceListHook();
            _controller.Start();
            if (!e.Args.Contains("--background")) ShowWindow();
        }
        catch (Exception ex)
        {
            MessageBox.Show("Battery Alert nu a putut porni.\n" + ex.Message, "Battery Alert", MessageBoxButton.OK, MessageBoxImage.Error);
            _exit = true;
            Shutdown(1);
        }
    }

    private void DeviceListHook() => HidSharp.DeviceList.Local.Changed += HidChanged;
    private void HidChanged(object? sender, EventArgs e) => Dispatcher.BeginInvoke(ScheduleRefresh);
    private void PowerChanged(object sender, PowerModeChangedEventArgs e)
    { if (e.Mode == PowerModes.Resume) Dispatcher.BeginInvoke(ScheduleRefresh); }
    private IntPtr WindowMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    { if (message == 0x0219) ScheduleRefresh(); return IntPtr.Zero; }
    private void ScheduleRefresh()
    { _deviceDebounce?.Stop(); _deviceDebounce?.Start(); }

    private void ShowWindow()
    {
        if (_window == null) return;
        _window.Show();
        if (_window.WindowState == WindowState.Minimized) _window.WindowState = WindowState.Normal;
        _window.Activate();
    }

    private static System.Drawing.Icon MakeTrayIcon()
    {
        using var bitmap = new System.Drawing.Bitmap(32, 32);
        using (var graphics = System.Drawing.Graphics.FromImage(bitmap))
        {
            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var background = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(19, 31, 49));
            using var accent = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(103, 232, 182));
            graphics.FillEllipse(background, 0, 0, 32, 32);
            graphics.FillRectangle(accent, 7, 10, 17, 12);
            graphics.FillRectangle(accent, 24, 13, 3, 6);
        }
        var handle = bitmap.GetHicon();
        try { using var borrowed = System.Drawing.Icon.FromHandle(handle); return (System.Drawing.Icon)borrowed.Clone(); }
        finally { DestroyIcon(handle); }
    }
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr handle);

    private static async Task DiagnoseAsync(string path)
    {
        var errors = new System.Collections.Concurrent.ConcurrentBag<string>();
        var poller = new ProviderPoller();
        poller.Error += errors.Add;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(25));
        try
        {
            var readings = await poller.PollAsync(MonitorController.CreateProviders(), timeout.Token);
            var inventory = HidInventory.Enumerate()
                .Where(e => e.Device.VendorID is 0x046d or 0x3554 or 0x3710 or 0x25a7)
                .Select(e => new { e.Product, e.HardwareId, e.UsagePage, e.Usage, e.InputLength, e.OutputLength, e.FeatureLength });
            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(new
            { GeneratedAt = DateTimeOffset.UtcNow, Readings = readings,
                VisibleDevices = DevicePolicy.Merge(readings).Where(r => DevicePolicy.Visible(r.Device)),
                Compatibility = DevicePolicy.Merge(readings), HidInventory = inventory, Errors = errors }, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        { await File.WriteAllTextAsync(path, JsonSerializer.Serialize(new { Error = ex.Message })); }
    }

    private async Task SmokeTestAsync(string path)
    {
        try
        {
            using var notifications = new NotificationService();
            using var controller = new MonitorController(Dispatcher, notifications,
                Path.Combine(Path.GetDirectoryName(Path.GetFullPath(path))!, "smoke-state"));
            await controller.RefreshAsync();
            var window = new MainWindow(controller);
            new WindowInteropHelper(window).EnsureHandle();
            window.Measure(new Size(1120, 850));
            window.Arrange(new Rect(0, 0, 1120, 850));
            window.UpdateLayout();
            foreach (var row in controller.Devices) _ = row.ProductImage;
            window.ShowSettingsForPreview();
            window.UpdateLayout();
            window.ShowCompatibilityForPreview(); window.UpdateLayout();
            if (controller.Devices.Any(r => !DevicePolicy.Visible(r.Device))) throw new InvalidOperationException("Non-wireless device in dashboard.");
            foreach (var kind in new[] { DeviceKind.Keyboard, DeviceKind.Mouse, DeviceKind.Headset })
            {
                controller.Filter(kind);
                if (controller.VisibleDevices.Cast<DeviceRow>().Any(r => r.Device.Kind != kind)) throw new InvalidOperationException("Category filter failed.");
            }
            controller.Filter(null);
            notifications.Show("Battery Alert — test fără sunet", false);
            File.WriteAllText(path, JsonSerializer.Serialize(new
            {
                Success = true, notifications.RegistrationError,
                Devices = controller.Devices.Select(row => row.Latest).ToArray(),
                Compatibility = controller.Compatibility.Select(row => new { row.Name, row.CompatibilityStatus, row.Evidence, row.Hardware }).ToArray(),
                controller.Status
            }, new JsonSerializerOptions { WriteIndented = true }));
            window.Close();
        }
        catch (Exception ex)
        {
            File.WriteAllText(path, JsonSerializer.Serialize(new { Success = false, Error = ex.ToString() }));
        }
    }

    private static void RenderPreview(string path, bool settings = false, bool small = false, bool compatibility = false)
    {
        var window = new MainWindow(null);
        var rows = new[] { PreviewRow("Logitech PRO X 2 LIGHTSPEED", "HeadsetControl", 47), PreviewRow("Pulsar Xlite V3", "Pulsar Fusion", 85),
            PreviewRow("Tastatură wireless · exemplu", "Bluetooth LE", 62), PreviewUnavailable("Powerbeat Pro") };
        window.DataContext = new
        {
            Devices = rows, VisibleDevices = rows, Compatibility = rows,
            DeviceCount = 4, LiveCount = 3, LowCount = 0, IsEmpty = false, IsFilterEmpty = false,
            MonitorLabel = "Monitorizare activă", PauseLabel = "Pauză",
            DraftSettings = new AppSettings { NtfyTopic = "battery-demo-preview" }, Status = "Monitorizare activă · interval 60 s", LastEvent = "Previzualizare cu date demonstrative"
        };
        if (settings) window.ShowSettingsForPreview();
        if (compatibility) window.ShowCompatibilityForPreview();
        var width = small ? 1060 : 1280; var height = small ? 720 : 960;
        var content = (FrameworkElement)window.Content;
        content.Measure(new Size(width, height)); content.Arrange(new Rect(0, 0, width, height)); content.UpdateLayout();
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            context.DrawRectangle(new SolidColorBrush(Color.FromRgb(16, 21, 27)), null, new Rect(0, 0, width, height));
        }
        bitmap.Render(visual);
        bitmap.Render(content);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(path); encoder.Save(file);
    }

    private static DeviceRow PreviewUnavailable(string name)
    {
        var descriptor = new DeviceDescriptor(name, name, "Windows", Kind: DeviceKind.Headset,
            Wireless: WirelessCapability.Confirmed, Connection: ConnectionKind.Bluetooth, Evidence: "Bluetooth · date demonstrative");
        var row = new DeviceRow(descriptor, new());
        row.Update(new(descriptor, null, null, Availability.Unsupported, DateTimeOffset.UtcNow));
        return row;
    }

    private static DeviceRow PreviewRow(string name, string provider, int level)
    {
        var kind = name.StartsWith("Tastatură") ? DeviceKind.Keyboard : provider == "HeadsetControl" ? DeviceKind.Headset : DeviceKind.Mouse;
        var descriptor = new DeviceDescriptor(name, name, provider, "Date demonstrative", Kind: kind,
            Wireless: WirelessCapability.Confirmed, Connection: kind == DeviceKind.Keyboard ? ConnectionKind.Bluetooth : ConnectionKind.UsbReceiver,
            Evidence: "Previzualizare · date demonstrative");
        var row = new DeviceRow(descriptor, new());
        row.Update(new(descriptor, level, false, Availability.Available, DateTimeOffset.UtcNow));
        return row;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _exit = true;
        _deviceDebounce?.Stop();
        SystemEvents.PowerModeChanged -= PowerChanged;
        HidSharp.DeviceList.Local.Changed -= HidChanged;
        _tray?.Dispose();
        _controller?.Dispose();
        _notifications?.Dispose();
        _showWait?.Unregister(null);
        _showEvent?.Dispose();
        _mutex?.Dispose();
        base.OnExit(e);
    }
}
