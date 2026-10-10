using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace BatteryAlert.App;

public partial class MainWindow : Window
{
    private readonly MonitorController? _controller;
    public MainWindow(MonitorController? controller)
    {
        InitializeComponent();
        _controller = controller;
        DataContext = controller;
        Icon = System.Windows.Media.Imaging.BitmapFrame.Create(new Uri("pack://application:,,,/Assets/app.ico"));
        SourceInitialized += (_, _) =>
        {
            var handle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            var dark = 1; var caption = 0x1B1510; var text = 0xF4F2EE;
            DwmSetWindowAttribute(handle, 20, ref dark, sizeof(int));
            DwmSetWindowAttribute(handle, 35, ref caption, sizeof(int));
            DwmSetWindowAttribute(handle, 36, ref text, sizeof(int));
        };
    }

    [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);

    private async void RefreshClick(object sender, RoutedEventArgs e)
    { if (_controller != null) await _controller.RefreshAsync(); }
    private void PauseClick(object sender, RoutedEventArgs e) => _controller?.TogglePause();
    private void SaveClick(object sender, RoutedEventArgs e)
    {
        try
        {
            UpdateTextBindings(this);
            if (HasValidationError(this)) throw new ArgumentException("Corectează valorile marcate înainte de salvare.");
            _controller?.SaveSettings();
            if (_controller != null) _ = _controller.RefreshAsync();
        }
        catch (Exception ex) { ShowError(ex); }
    }

    private static void UpdateTextBindings(DependencyObject element)
    {
        if (element is TextBox text) text.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(element); i++)
            UpdateTextBindings(VisualTreeHelper.GetChild(element, i));
    }

    private void DevicesPageClick(object sender, RoutedEventArgs e)
    {
        if (DevicesPage == null || SettingsPage == null) return;
        DevicesPage.Visibility = Visibility.Visible; SettingsPage.Visibility = Visibility.Collapsed; CompatibilityPage.Visibility = Visibility.Collapsed;
        PageTitle.Text = "Dispozitivele tale"; MainScroll.ScrollToTop();
    }
    private void SettingsPageClick(object sender, RoutedEventArgs e)
    {
        if (DevicesPage == null || SettingsPage == null) return;
        DevicesPage.Visibility = Visibility.Collapsed; SettingsPage.Visibility = Visibility.Visible; CompatibilityPage.Visibility = Visibility.Collapsed;
        PageTitle.Text = "Alerte și telefon"; MainScroll.ScrollToTop();
    }
    internal void ShowSettingsForPreview() => SettingsNav.IsChecked = true;
    internal void ShowCompatibilityForPreview() => CompatibilityNav.IsChecked = true;
    private void CompatibilityPageClick(object sender, RoutedEventArgs e)
    {
        if (CompatibilityPage == null) return;
        DevicesPage.Visibility = Visibility.Collapsed; SettingsPage.Visibility = Visibility.Collapsed;
        CompatibilityPage.Visibility = Visibility.Visible; PageTitle.Text = "Compatibilitate"; MainScroll.ScrollToTop();
    }
    private void FilterClick(object sender, RoutedEventArgs e)
    {
        if (sender is not RadioButton button) return;
        DeviceKind? kind = Enum.TryParse<DeviceKind>(Convert.ToString(button.Tag), out var parsed) ? parsed : null;
        _controller?.Filter(kind);
    }
    private void ChooseImageClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not DeviceRow row) return;
        var picker = new Microsoft.Win32.OpenFileDialog
        { Title = "Alege fotografia dispozitivului", Filter = "Fotografii|*.png;*.jpg;*.jpeg;*.bmp", CheckFileExists = true };
        if (picker.ShowDialog(this) != true) return;
        try { row.Settings.ProductImageFile = ProductArtwork.Import(picker.FileName); row.RefreshArtwork(); }
        catch (Exception ex) { ShowError(ex); }
    }
    private void ResetImageClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not DeviceRow row) return;
        row.Settings.ProductImageFile = null; row.RefreshArtwork();
    }

    private static bool HasValidationError(DependencyObject element)
    {
        if (Validation.GetHasError(element)) return true;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(element); i++)
            if (HasValidationError(VisualTreeHelper.GetChild(element, i))) return true;
        return false;
    }

    private void CopyTopicClick(object sender, RoutedEventArgs e)
    {
        if (_controller == null) return;
        try { Clipboard.SetText(NtfyClient.TopicUri(_controller.DraftSettings).AbsoluteUri); }
        catch (Exception ex) { ShowError(ex); }
    }
    private void TestDesktopClick(object sender, RoutedEventArgs e)
    { try { _controller?.TestDesktop(); } catch (Exception ex) { ShowError(ex); } }
    private async void TestPhoneClick(object sender, RoutedEventArgs e)
    {
        if (_controller == null) return;
        var button = (Button)sender;
        button.IsEnabled = false;
        try
        {
            await _controller.TestPhoneAsync();
            MessageBox.Show(this, "Mesajul de test a fost acceptat de serverul ntfy. Verifică telefonul.", "Battery Alert");
        }
        catch (Exception ex) { ShowError(ex); }
        finally { button.IsEnabled = true; }
    }
    private void DiagnosticClick(object sender, RoutedEventArgs e)
    {
        var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BatteryAlert");
        Directory.CreateDirectory(directory);
        Process.Start(new ProcessStartInfo(directory) { UseShellExecute = true });
    }
    private void ShowError(Exception ex) => MessageBox.Show(this, ex.Message, "Battery Alert", MessageBoxButton.OK, MessageBoxImage.Warning);
}
