using BatteryAlert.Core;
using Xunit;

namespace BatteryAlert.Tests;

public sealed class WirelessTests
{
    private static DeviceDescriptor Device(string id = "one", DeviceKind kind = DeviceKind.Mouse,
        WirelessCapability wireless = WirelessCapability.Confirmed, ConnectionKind connection = ConnectionKind.UsbReceiver,
        string? physical = null) => new(id, "Peripheral", "test", "046D:C52B", Kind: kind, Wireless: wireless, Connection: connection, PhysicalId: physical);
    private static BatteryReading Reading(DeviceDescriptor d, int? percent = 10) => new(d, percent, false,
        percent.HasValue ? Availability.Available : Availability.Unsupported, DateTimeOffset.UtcNow);

    [Theory]
    [InlineData(DeviceKind.Headset)] [InlineData(DeviceKind.Mouse)] [InlineData(DeviceKind.Keyboard)]
    public void WirelessCategoriesStayVisibleDuringCableCharging(DeviceKind kind)
    {
        var d = Device(kind: kind, connection: ConnectionKind.UsbCable);
        Assert.True(DevicePolicy.Visible(d));
        var reading = Reading(d) with { Charging = true };
        Assert.Null(AlertEngine.Evaluate(reading, new(), new(), DateTimeOffset.UtcNow));
    }
    [Theory]
    [InlineData(DeviceKind.Mouse, WirelessCapability.WiredOnly)]
    [InlineData(DeviceKind.Keyboard, WirelessCapability.Unknown)]
    [InlineData(DeviceKind.Other, WirelessCapability.Confirmed)]
    [InlineData(DeviceKind.Unknown, WirelessCapability.Confirmed)]
    public void WiredUnknownAndOtherCategoriesCannotEnterDashboard(DeviceKind kind, WirelessCapability wireless)
        => Assert.False(DevicePolicy.Visible(Device(kind: kind, wireless: wireless)));

    [Theory]
    [InlineData(0x0540u, DeviceKind.Keyboard)] [InlineData(0x0580u, DeviceKind.Mouse)]
    [InlineData(0x0404u, DeviceKind.Headset)] [InlineData(0x0418u, DeviceKind.Headset)]
    [InlineData(0x020Cu, DeviceKind.Other)] [InlineData(0x0508u, DeviceKind.Other)]
    public void BluetoothClassDoesNotTreatPhonesOrControllersAsHeadphones(uint cod, DeviceKind kind)
        => Assert.Equal(kind, DevicePolicy.BluetoothClass(cod));

    [Fact]
    public void SameReceiverCanOwnDifferentBatteriesAndHardwareIdIsNotIdentity()
    {
        var readings = new[] { Reading(Device("slot1", physical: "receiver:1")), Reading(Device("slot2", physical: "receiver:2")),
            Reading(Device("no-serial1")), Reading(Device("no-serial2")) };
        Assert.Equal(4, DevicePolicy.Merge(readings).Count);
    }
    [Fact]
    public void PositiveIdentityDeduplicatesAndMigratesSettingsLatchesAndOutbox()
    {
        var state = new SavedState(); state.Settings.Devices["old"] = new() { Threshold = 23, Sound = false, ProductImageFile = "photo.png" };
        state.Alerts["old"] = new() { Alerted = true, Threshold = 23 };
        state.Pending.Add(new() { Alert = new("alert", "old", "Mouse", 9, DateTimeOffset.UtcNow) });
        var merged = Assert.Single(DevicePolicy.Merge([Reading(Device("old", physical: "unit"), null), Reading(Device("new", physical: "unit"), 9)]));
        DevicePolicy.PreserveState(state, merged.Device);
        Assert.Equal(23, state.Settings.For(merged.Device.Id).Threshold);
        Assert.False(state.Settings.For(merged.Device.Id).Sound);
        Assert.Equal("photo.png", state.Settings.For(merged.Device.Id).ProductImageFile);
        Assert.True(state.Alerts[merged.Device.Id].Alerted);
        Assert.Equal(merged.Device.Id, state.Pending[0].Alert.DeviceId);
        Assert.Null(AlertEngine.Evaluate(merged, state.Settings.For(merged.Device.Id), state.Alerts[merged.Device.Id], DateTimeOffset.UtcNow));
        state.Alerts[merged.Device.Id].Alerted = false;
        DevicePolicy.PreserveState(state, merged.Device);
        Assert.False(state.Alerts[merged.Device.Id].Alerted); // Legacy latch cannot undo a later rearm.
    }
    [Fact]
    public void CanonicalIdentityDoesNotChangeWhenProviderBecomesUnavailable()
    {
        var a = Reading(Device("a", physical: "unit")); var b = Reading(Device("b", physical: "unit"));
        Assert.Equal(Assert.Single(DevicePolicy.Merge([a, b with { Percent = null, Availability = Availability.Unavailable }])).Device.Id,
            Assert.Single(DevicePolicy.Merge([a with { Percent = null, Availability = Availability.Unavailable }, b])).Device.Id);
    }
    [Fact]
    public void PulsarCableModeDoesNotMeanWiredOnly()
    {
        var crazy = PulsarModelCatalog.Classify(new("c", "Pulsar Xlite CrazyLight", "Pulsar"), 0x3710, 0x3506);
        Assert.Equal(ConnectionKind.UsbCable, crazy.Connection); Assert.True(DevicePolicy.Visible(crazy));
        var unknown = PulsarModelCatalog.Classify(new("u", "USB mouse", "Pulsar"), 0x3710, 0x3506);
        Assert.False(DevicePolicy.Visible(unknown));
        Assert.True(DevicePolicy.Visible(PulsarModelCatalog.Classify(new("r", "Xlite V3", "Pulsar"), 0x3554, 0xF508)));
    }
    [Theory]
    [InlineData("05 01 09 06 A1 01 C0", DeviceKind.Keyboard)]
    [InlineData("05 01 09 02 A1 01 09 01 A1 00 C0 C0", DeviceKind.Mouse)]
    [InlineData("05 01 09 05 A1 01 C0", DeviceKind.Other)]
    [InlineData("05 01 09 06 A1 01", DeviceKind.Unknown)]
    [InlineData("05", DeviceKind.Unknown)]
    [InlineData("FE 05", DeviceKind.Unknown)]
    public void BleHidReportMapRejectsTruncationAndControllers(string hex, DeviceKind kind)
        => Assert.Equal(kind, HidReportMap.Kind(Convert.FromHexString(hex.Replace(" ", ""))));
    [Fact]
    public void BleBatteryRequiresExactlyOneValidByte()
    {
        Assert.True(DevicePolicy.TryBlePercent([0], out var zero)); Assert.Equal(0, zero);
        Assert.True(DevicePolicy.TryBlePercent([100], out _));
        Assert.False(DevicePolicy.TryBlePercent([], out _)); Assert.False(DevicePolicy.TryBlePercent([101], out _));
        Assert.False(DevicePolicy.TryBlePercent([10, 0], out _));
    }
    [Fact]
    public void HidppRepliesAreMatchedToSlotFunctionAndSoftwareId()
    {
        var request = HidppProtocol.Request(2, 5, 0x10); var response = (byte[])request.Clone();
        Assert.True(HidppProtocol.Matches(response, request));
        response[1] = 1; Assert.False(HidppProtocol.Matches(response, request));
        response[1] = 2; response[3] = 0x1B; Assert.False(HidppProtocol.Matches(response, request));
        Assert.False(HidppProtocol.Matches(request.AsSpan(0, 7), request));
        Assert.False(HidppProtocol.IsReceiver(0x046D, 0xC232));
    }
    [Fact]
    public void HidppCoarseBatteryAndLegacyZeroNeverInventPercentages()
    {
        Assert.True(HidppProtocol.TryBattery(0x1004, [0, 4, 0, 0], false, out var percent, out _)); Assert.Null(percent);
        Assert.True(HidppProtocol.TryBattery(0x1000, [0, 0, 0], true, out percent, out _)); Assert.Null(percent);
        Assert.True(HidppProtocol.TryBattery(0x1004, [0, 0, 0, 0], true, out percent, out _)); Assert.Equal(0, percent);
        Assert.True(HidppProtocol.TryBattery(0x1000, [9, 0, 4], true, out percent, out var charging)); Assert.True(charging);
        Assert.False(HidppProtocol.TryBattery(0x1004, [101, 1, 0, 0], true, out _, out _));
        Assert.False(HidppProtocol.TryBattery(0x1000, [9, 0, 5], true, out _, out _));
        Assert.False(HidppProtocol.TryBattery(0x1004, [9], true, out _, out _));
    }
    [Fact]
    public void HeadsetUsbDacDoesNotBecomeWirelessBecauseProviderSupportsBatteryCommands()
    {
        var wired = Assert.Single(HeadsetJson.Parse("""{"devices":[{"device":"G PRO X","id_vendor":"0x046d","id_product":"0x0aaa","battery":{"status":"BATTERY_UNAVAILABLE","level":0}}]}""", DateTimeOffset.UtcNow));
        Assert.False(DevicePolicy.Visible(wired.Device));
        Assert.Equal(WirelessCapability.WiredOnly, wired.Device.Wireless);
    }
    [Fact]
    public void KeyboardUsesItsOwnIllustrationEvenWithPulsarOrLogitechBrand()
    { Assert.Equal("Tastatură wireless", ProductIdentity.Resolve(Device() with { Kind = DeviceKind.Keyboard, Name = "Pulsar Xboard" }).Category); }
    [Fact]
    public void WiredFamilyAndUnknownHeadsetCannotBePromotedByBatteryValue()
    {
        Assert.False(DevicePolicy.Visible(HeadsetCatalog.Classify(new("dac", "Logitech G633/G635/G733/G933/G935", "HeadsetControl", "0x046d:0x0a5c"))));
        Assert.True(DevicePolicy.Visible(HeadsetCatalog.Classify(new("wireless", "Logitech G633/G635/G733/G933/G935", "HeadsetControl", "0x046d:0x0a5b"))));
        Assert.False(DevicePolicy.Visible(HeadsetCatalog.Classify(new("unknown", "Unknown headset", "HeadsetControl", "0x1234:0x5678"))));
        Assert.True(DevicePolicy.Visible(HeadsetCatalog.Classify(new("air", "ROCCAT Elo 7.1 Air", "HeadsetControl", "0x1e7d:0x3a37"))));
    }
    [Fact]
    public void MigratedStateSurvivesDiskRestartWithoutRepeatingAlert()
    {
        var path = Path.Combine(Path.GetTempPath(), "battery-wireless-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var store = new StateStore(path); var old = new SavedState();
            old.Settings.Devices["legacy"] = new() { Threshold = 15 }; old.Alerts["legacy"] = new() { Alerted = true, Threshold = 15 };
            store.Save(old); var state = store.Load();
            var reading = Assert.Single(DevicePolicy.Merge([Reading(Device("legacy", physical: "same-unit"), 9)]));
            DevicePolicy.PreserveState(state, reading.Device); store.Save(state); var restarted = store.Load();
            Assert.Null(AlertEngine.Evaluate(reading, restarted.Settings.For(reading.Device.Id), restarted.Alerts[reading.Device.Id], DateTimeOffset.UtcNow));
        }
        finally { File.Delete(path); File.Delete(path + ".bak"); }
    }
    [Fact]
    public void MergingAliasesDoesNotDeliverTwoPendingPhoneAlertsForOneBattery()
    {
        var state = new SavedState(); var now = DateTimeOffset.UtcNow;
        state.Pending.Add(new() { Alert = new("a", "receiver", "Mouse", 9, now.AddSeconds(-1)) });
        state.Pending.Add(new() { Alert = new("b", "cable", "Mouse", 8, now) });
        var merged = Assert.Single(DevicePolicy.Merge([Reading(Device("receiver", physical: "unit")), Reading(Device("cable", physical: "unit"))]));
        DevicePolicy.PreserveState(state, merged.Device);
        Assert.Equal("b", Assert.Single(state.Pending).Alert.Id);
    }
}
