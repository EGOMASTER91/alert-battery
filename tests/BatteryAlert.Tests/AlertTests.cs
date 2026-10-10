using BatteryAlert.Core;
using Xunit;

namespace BatteryAlert.Tests;

public sealed class AlertTests
{
    private static readonly DeviceDescriptor Device = new("test:1", "Căști", "Test");
    private static BatteryReading Reading(int? percent, bool? charging = false, Availability availability = Availability.Available,
        DateTimeOffset? at = null) => new(Device, percent, charging, availability, at ?? DateTimeOffset.UtcNow);

    [Theory]
    [InlineData(0, true)] [InlineData(9, true)] [InlineData(10, true)] [InlineData(11, false)] [InlineData(100, false)]
    public void FirstValidReadingUsesInclusiveThreshold(int percent, bool expected)
    { Assert.Equal(expected, AlertEngine.Evaluate(Reading(percent), new(), new(), DateTimeOffset.UtcNow) != null); }

    [Fact]
    public void SuppressesDuplicatesUntilFivePointRecovery()
    {
        var state = new AlertState(); var settings = new DeviceSettings(); var now = DateTimeOffset.UtcNow;
        Assert.NotNull(AlertEngine.Evaluate(Reading(9), settings, state, now));
        foreach (var level in new[] { 9, 8, 11, 10, 14, 7 }) Assert.Null(AlertEngine.Evaluate(Reading(level), settings, state, now));
        Assert.Null(AlertEngine.Evaluate(Reading(15), settings, state, now));
        Assert.NotNull(AlertEngine.Evaluate(Reading(10), settings, state, now));
    }

    [Fact]
    public void ChargingAndUnavailableNeverBecomeFalseLowBattery()
    {
        var state = new AlertState(); var settings = new DeviceSettings(); var now = DateTimeOffset.UtcNow;
        Assert.Null(AlertEngine.Evaluate(Reading(4, true), settings, state, now));
        Assert.Null(AlertEngine.Evaluate(Reading(null, null, Availability.Unavailable), settings, state, now));
        Assert.Null(AlertEngine.Evaluate(Reading(0, false, Availability.Unsupported), settings, state, now));
        Assert.Null(AlertEngine.Evaluate(Reading(7, at: now.AddMinutes(-3)), settings, state, now));
        Assert.Null(AlertEngine.Evaluate(Reading(101), settings, state, now));
        Assert.NotNull(AlertEngine.Evaluate(Reading(4, false), settings, state, now));
    }

    [Fact]
    public void NoChannelOrDisabledDeviceDoesNotConsumeAlert()
    {
        var state = new AlertState(); var now = DateTimeOffset.UtcNow;
        Assert.Null(AlertEngine.Evaluate(Reading(9), new() { Desktop = false, Phone = false }, state, now));
        Assert.Null(AlertEngine.Evaluate(Reading(9), new() { Enabled = false }, state, now));
        Assert.False(state.Alerted);
    }

    [Fact]
    public void SeparateDevicesAndThresholdChangesHaveIndependentLatches()
    {
        var settings = new DeviceSettings(); var state = new AlertState(); var now = DateTimeOffset.UtcNow;
        Assert.NotNull(AlertEngine.Evaluate(Reading(9), settings, state, now));
        Assert.NotNull(AlertEngine.Evaluate(Reading(9), settings, new(), now));
        settings.Threshold = 5;
        Assert.Null(AlertEngine.Evaluate(Reading(9), settings, state, now));
        Assert.NotNull(AlertEngine.Evaluate(Reading(5), settings, state, now));
    }

    [Fact]
    public void RestartKeepsAlertLatchAndOutbox()
    {
        var folder = Path.Combine(Path.GetTempPath(), "BatteryAlert-tests-" + Guid.NewGuid());
        Directory.CreateDirectory(folder);
        try
        {
            var store = new StateStore(Path.Combine(folder, "state.json")); var saved = new SavedState();
            var state = new AlertState(); saved.Alerts[Device.Id] = state;
            var alert = AlertEngine.Evaluate(Reading(9), new(), state, DateTimeOffset.UtcNow)!;
            saved.Pending.Add(new() { Alert = alert });
            store.Save(saved); var loaded = store.Load();
            Assert.Null(AlertEngine.Evaluate(Reading(8), new(), loaded.Alerts[Device.Id], DateTimeOffset.UtcNow));
            Assert.Equal(alert, Assert.Single(loaded.Pending).Alert);
            Assert.Equal(saved.Settings.NtfyTopic, loaded.Settings.NtfyTopic);
        }
        finally { Directory.Delete(folder, true); }
    }

    [Fact]
    public void NullStateEntriesAreRecoveredInsteadOfCrashingStartup()
    {
        var folder = Path.Combine(Path.GetTempPath(), "BatteryAlert-tests-" + Guid.NewGuid());
        Directory.CreateDirectory(folder);
        try
        {
            var store = new StateStore(Path.Combine(folder, "state.json"));
            File.WriteAllText(store.Path, """{"Settings":{"Devices":{"bad":null}}}""");
            Assert.Empty(store.Load().Settings.Devices);
            Assert.NotNull(store.RecoveryMessage);
            Assert.Single(Directory.GetFiles(folder, "state.json.corrupt-*"));
        }
        finally { Directory.Delete(folder, true); }
    }

    [Fact]
    public void CorruptStateRecoversBackup()
    {
        var folder = Path.Combine(Path.GetTempPath(), "BatteryAlert-tests-" + Guid.NewGuid());
        Directory.CreateDirectory(folder);
        try
        {
            var store = new StateStore(Path.Combine(folder, "state.json")); var saved = new SavedState();
            saved.Alerts[Device.Id] = new() { Alerted = true };
            store.Save(saved); store.Save(saved);
            File.WriteAllText(store.Path, "not json");
            Assert.True(store.Load().Alerts[Device.Id].Alerted);
            Assert.NotNull(store.RecoveryMessage);
        }
        finally { Directory.Delete(folder, true); }
    }
}
