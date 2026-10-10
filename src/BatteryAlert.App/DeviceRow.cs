using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace BatteryAlert.App;

public sealed class DeviceRow(DeviceDescriptor device, DeviceSettings settings) : INotifyPropertyChanged
{
    public DeviceDescriptor Device { get; private set; } = device;
    public DeviceSettings Settings { get; } = settings;
    public BatteryReading? Latest { get; private set; }
    private int? _lastPercent;
    private DateTimeOffset? _lastGood;
    public string Name => Device.Name;
    public string Provider => Device.Provider;
    public string Hardware => Device.HardwareId ?? "Bluetooth / Windows";
    public string BatteryText => Latest?.IsValid == true ? $"{Latest.Percent}%" : _lastPercent.HasValue ? $"{_lastPercent}% · vechi" : "—";
    public string Status => Latest == null ? "În așteptare" : Latest.Availability == Availability.Unsupported
        ? "Citirea bateriei nu este suportată" : Latest.IsValid ? Latest.Charging == true ? "Se încarcă" : "Conectat"
        : _lastPercent.HasValue ? "Indisponibil · ultima valoare este veche" : "Dispozitiv oprit sau indisponibil";
    public string Detail => Latest?.Detail ?? "";
    public string LastRead => _lastGood?.ToLocalTime().ToString("HH:mm:ss") ?? "—";
    public ProductIdentity Identity => ProductIdentity.Resolve(Device);
    public string Brand => Identity.Brand;
    public string Category => Device.Kind == DeviceKind.Unknown ? Identity.Category : DevicePolicy.Category(Device.Kind);
    public string Connection => DevicePolicy.Connection(Device.Connection);
    public string Evidence => Device.Evidence ?? "Identificare neconfirmată";
    public string CompatibilityStatus => DevicePolicy.Visible(Device) ? IsLive ? "Citire validată acum" : Status : DevicePolicy.Exclusion(Device);
    public ImageSource ProductImage => ProductArtwork.Resolve(Identity, Settings.ProductImageFile);
    public double PhotoZoom => Settings.ProductImageFile != null || Identity.ImageFile == null || Identity.Brand == "BEATS" ? 1
        : Identity.Brand == "LOGITECH G" ? 1.5 : 1.2;
    public string ImageCaption => Settings.ProductImageFile != null ? "Fotografia ta" : Identity.ImageFile != null ? "Imagine de produs · variantă reprezentativă" : "Ilustrație generică · poți adăuga o fotografie";
    public string BatteryValue => (Latest?.IsValid == true ? Latest.Percent : _lastPercent)?.ToString() ?? "—";
    public string PercentSuffix => Latest?.IsValid == true || _lastPercent.HasValue ? "%" : "";
    public double BatteryLevel => Latest?.IsValid == true ? Latest.Percent ?? 0 : _lastPercent ?? 0;
    public string BatteryNote => Latest?.Availability == Availability.Unsupported ? "Citirea bateriei nu este suportată"
        : Latest?.IsValid != true ? _lastPercent.HasValue ? "Ultima valoare · veche" : "Baterie indisponibilă"
        : Latest.Charging == true ? "Se încarcă" : Latest.Percent <= Settings.Threshold ? "Baterie scăzută" : "Baterie disponibilă";
    public string LastReadLabel => _lastGood.HasValue ? $"Citit la {_lastGood.Value.ToLocalTime():dd.MM · HH:mm:ss}" : "Nicio citire validă";
    public bool IsLive => Latest?.IsValid == true;
    public bool NeedsAttention => IsLive && Latest?.Charging != true && Latest?.Percent <= Settings.Threshold;
    public Brush BatteryBrush => Brush(NeedsAttention ? "#F4AF78" : IsLive ? "#83DBC8" : "#89969E");
    public Brush CardBorderBrush => Brush(NeedsAttention ? "#73513C" : "#2B333B");
    public void RefreshArtwork() => OnPropertyChanged("");
    private static SolidColorBrush Brush(string value)
    { var result = new SolidColorBrush((Color)ColorConverter.ConvertFromString(value)); result.Freeze(); return result; }
    public event PropertyChangedEventHandler? PropertyChanged;

    public void Update(BatteryReading reading)
    {
        Device = reading.Device;
        Latest = reading;
        if (reading.IsValid) { _lastPercent = reading.Percent; _lastGood = reading.ReadAt; }
        OnPropertyChanged("");
    }
    private void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));
}
