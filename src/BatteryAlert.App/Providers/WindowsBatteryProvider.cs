using Windows.Devices.Enumeration;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Storage.Streams;

namespace BatteryAlert.App.Providers;

public sealed class WindowsBatteryProvider : IBatteryProvider
{
    private readonly Dictionary<string, BatteryReading> _readings = [];
    public string Name => "Windows / Bluetooth";
    private const string BatteryProperty = "{104EA319-6EE2-4701-BD47-8DDBF425BBE5} 2";
    private const string ConnectedProperty = "System.Devices.Aep.IsConnected";
    private const string ContainerProperty = "System.Devices.Aep.ContainerId";
    private static readonly string[] Properties = [BatteryProperty, ConnectedProperty, ContainerProperty,
        "System.Devices.Aep.Bluetooth.Le.Appearance"];

    public async Task<IReadOnlyList<DeviceDescriptor>> DiscoverAsync(CancellationToken ct)
    {
        var classicTask = DeviceInformation.FindAllAsync(BluetoothDevice.GetDeviceSelectorFromPairingState(true), Properties, DeviceInformationKind.AssociationEndpoint).AsTask(ct);
        var bleTask = DeviceInformation.FindAllAsync(BluetoothLEDevice.GetDeviceSelectorFromPairingState(true), Properties, DeviceInformationKind.AssociationEndpoint).AsTask(ct);
        var nodes = await WindowsInventory.SnapshotAsync(ct);
        var classic = await classicTask; var lowEnergy = await bleTask;
        var updated = new Dictionary<string, BatteryReading>();
        foreach (var item in classic.Concat(lowEnergy).DistinctBy(i => i.Id))
        {
            ct.ThrowIfCancellationRequested();
            var ble = lowEnergy.Any(i => i.Id == item.Id);
            item.Properties.TryGetValue(ContainerProperty, out var container);
            var children = nodes.Where(n => container != null && string.Equals(Convert.ToString(container), n.Container, StringComparison.OrdinalIgnoreCase)).ToArray();
            var kind = children.Select(n => n.Kind).FirstOrDefault(k => k != DeviceKind.Unknown);
            var evidence = kind == DeviceKind.Unknown ? "Bluetooth; categoria neconfirmată" : "Categoria perifericului din nodurile Windows";
            if (kind == DeviceKind.Unknown && children.Any(n => n.Instance.Contains("0000111e-", StringComparison.OrdinalIgnoreCase)
                || n.Instance.Contains("00001108-", StringComparison.OrdinalIgnoreCase)))
            { kind = DeviceKind.Headset; evidence = "Service Bluetooth Hands-Free / Headset din nodurile Windows"; }
            if (kind == DeviceKind.Unknown && item.Properties.TryGetValue("System.Devices.Aep.Bluetooth.Le.Appearance", out var appearance) && appearance != null)
            { kind = DevicePolicy.BleAppearance(Convert.ToUInt16(appearance)); evidence = "Bluetooth LE Appearance"; }
            var connected = item.Properties.TryGetValue(ConnectedProperty, out var connection) && connection is true;
            if (!ble)
            {
                try
                {
                    using var bluetooth = await BluetoothDevice.FromIdAsync(item.Id).AsTask(ct);
                    if (bluetooth != null)
                    {
                        connected = bluetooth.ConnectionStatus == BluetoothConnectionStatus.Connected;
                        if (kind == DeviceKind.Unknown) { kind = DevicePolicy.BluetoothClass(bluetooth.ClassOfDevice.RawValue); evidence = "Bluetooth Class of Device"; }
                    }
                }
                catch (Exception) when (!ct.IsCancellationRequested) { }
            }
            var id = "windows:" + item.Id;
            var previous = _readings.GetValueOrDefault(id);
            if (kind == DeviceKind.Unknown && previous?.Device.Kind is DeviceKind.Headset or DeviceKind.Mouse or DeviceKind.Keyboard)
            { kind = previous.Device.Kind; evidence = previous.Device.Evidence ?? evidence; }
            var physical = container != null && Guid.TryParse(Convert.ToString(container), out var guid) && guid != Guid.Empty ? "container:" + guid.ToString("D") : null;
            var descriptor = new DeviceDescriptor(id, item.Name, Name, Path: item.Id, Kind: kind,
                Wireless: WirelessCapability.Confirmed, Connection: ConnectionKind.Bluetooth, Evidence: evidence, PhysicalId: physical);
            item.Properties.TryGetValue(BatteryProperty, out var raw);
            raw ??= children.Select(n => n.Properties.GetValueOrDefault(BatteryProperty)).FirstOrDefault(v => v != null);
            int? level = null;
            try { if (raw != null && Convert.ToInt32(raw) is >= 0 and <= 100) level = Convert.ToInt32(raw); }
            catch (Exception ex) when (ex is FormatException or InvalidCastException or OverflowException) { }
            var detail = raw == null ? "Citirea bateriei nu este suportată" : connected ? "Raportat de Windows" : "Deconectat";
            var availability = !connected ? Availability.Unavailable : level.HasValue ? Availability.Available : Availability.Unsupported;
            if (ble && connected && kind != DeviceKind.Other && (!level.HasValue || kind == DeviceKind.Unknown))
            {
                var gatt = await ReadBleAsync(item.Id, kind, ct);
                if (kind == DeviceKind.Unknown && gatt.Kind != DeviceKind.Unknown) descriptor = descriptor with { Kind = gatt.Kind, Evidence = "Bluetooth LE HID Report Map" };
                if (gatt.Percent.HasValue) { level = gatt.Percent; availability = Availability.Available; detail = "Bluetooth LE Battery Level"; }
                else if (!level.HasValue) { availability = gatt.Availability; detail = gatt.Detail; }
            }
            updated[id] = new(descriptor, connected ? level : null, null, availability, DateTimeOffset.UtcNow, detail);
        }
        _readings.Clear(); foreach (var pair in updated) _readings[pair.Key] = pair.Value;
        return _readings.Values.Select(r => r.Device).ToArray();
    }

    private static async Task<(int? Percent, DeviceKind Kind, Availability Availability, string Detail)> ReadBleAsync(string id, DeviceKind kind, CancellationToken ct)
    {
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(ct); budget.CancelAfter(TimeSpan.FromSeconds(2));
        try
        {
            using var device = await BluetoothLEDevice.FromIdAsync(id).AsTask(budget.Token);
            if (device == null) return (null, DeviceKind.Unknown, Availability.Unavailable, "Bluetooth indisponibil sau acces refuzat");
            if (kind == DeviceKind.Unknown) kind = DevicePolicy.BleAppearance(device.Appearance.RawValue);
            if (kind == DeviceKind.Unknown)
            {
                var hid = await device.GetGattServicesForUuidAsync(GattServiceUuids.HumanInterfaceDevice, BluetoothCacheMode.Uncached).AsTask(budget.Token);
                foreach (var service in hid.Services)
                {
                    using (service)
                    {
                        var maps = await service.GetCharacteristicsForUuidAsync(new Guid("00002a4b-0000-1000-8000-00805f9b34fb"), BluetoothCacheMode.Uncached).AsTask(budget.Token);
                        foreach (var map in maps.Characteristics)
                        {
                            var report = await map.ReadValueAsync(BluetoothCacheMode.Uncached).AsTask(budget.Token);
                            if (report.Status == GattCommunicationStatus.Success) kind = HidReportMap.Kind(Bytes(report.Value));
                        }
                    }
                }
            }
            if (kind is DeviceKind.Unknown or DeviceKind.Other) return (null, kind, Availability.Unsupported, "Categoria nu este confirmată pentru monitorizarea bateriei");
            var services = await device.GetGattServicesForUuidAsync(GattServiceUuids.Battery, BluetoothCacheMode.Uncached).AsTask(budget.Token);
            if (services.Status != GattCommunicationStatus.Success) return (null, kind, Availability.Unavailable, "Serviciul BLE nu răspunde sau accesul este refuzat");
            var levels = new List<int>();
            foreach (var service in services.Services)
            {
                using (service)
                {
                    var characteristics = await service.GetCharacteristicsForUuidAsync(GattCharacteristicUuids.BatteryLevel, BluetoothCacheMode.Uncached).AsTask(budget.Token);
                    foreach (var characteristic in characteristics.Characteristics)
                    {
                        var value = await characteristic.ReadValueAsync(BluetoothCacheMode.Uncached).AsTask(budget.Token);
                        if (value.Status == GattCommunicationStatus.Success && DevicePolicy.TryBlePercent(Bytes(value.Value), out var percent)) levels.Add(percent);
                    }
                }
            }
            return levels.Count > 0 ? (levels.Min(), kind, Availability.Available, "Bluetooth LE Battery Level")
                : (null, kind, services.Services.Count > 0 ? Availability.Unavailable : Availability.Unsupported,
                    services.Services.Count > 0 ? "Răspuns BLE lipsă sau invalid" : "Citirea bateriei nu este suportată");
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { return (null, DeviceKind.Unknown, Availability.Unavailable, "Citirea BLE a expirat"); }
        catch (Exception) when (!ct.IsCancellationRequested) { return (null, DeviceKind.Unknown, Availability.Unavailable, "Citirea BLE a eșuat sau accesul este refuzat"); }
    }
    private static byte[] Bytes(IBuffer buffer)
    { var bytes = new byte[buffer.Length]; using var reader = DataReader.FromBuffer(buffer); reader.ReadBytes(bytes); return bytes; }
    public Task<BatteryReading> ReadAsync(DeviceDescriptor device, CancellationToken ct)
    { ct.ThrowIfCancellationRequested(); return Task.FromResult(_readings.GetValueOrDefault(device.Id) ?? BatteryReading.Unavailable(device)); }
}
