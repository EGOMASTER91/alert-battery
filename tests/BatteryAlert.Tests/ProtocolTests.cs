using BatteryAlert.Core;
using System.Text.Json;
using Xunit;

namespace BatteryAlert.Tests;

public sealed class ProtocolTests
{
    [Theory]
    [InlineData(0, false)] [InlineData(9, false)] [InlineData(100, true)]
    public void PulsarReadsValidatedFrames(int level, bool charging)
    {
        var frame = Frame(level, charging);
        Assert.True(PulsarProtocol.TryParse(frame, out var percent, out var plugged));
        Assert.Equal(level, percent); Assert.Equal(charging, plugged);
    }

    private static byte[] Frame(int level, bool charging)
    {
        var frame = PulsarProtocol.PowerRequest(); frame[6] = (byte)level; frame[7] = charging ? (byte)1 : (byte)0;
        frame[8] = 0x0e; frame[9] = 0x74;
        frame[16] = PulsarProtocol.Checksum(frame.AsSpan(0, 16)); return frame;
    }

    [Fact]
    public void CapturedXliteV3PacketReportsRealBattery()
    {
        // Captured on 3554:F508 without Fusion running, 2026-10-10.
        var packet = Convert.FromHexString("08040000000255000FCA00000000000019");
        Assert.True(PulsarProtocol.TryParse(packet, out var percent, out var charging));
        Assert.Equal(85, percent);
        Assert.False(charging);
    }

    [Fact]
    public void CatalogRestrictsProtocolsToExplicitMouseIds()
    {
        Assert.True(PulsarModelCatalog.Fusion(0x3554, 0xf508));
        Assert.True(PulsarModelCatalog.CrazyLight(0x3710, 0x5406));
        Assert.True(PulsarModelCatalog.CrazyLight(0x3710, 0x5502));
        Assert.False(PulsarModelCatalog.Supported(0x3710, 0x2404)); // keyboard, not mouse
        Assert.False(PulsarModelCatalog.Supported(0x3710, 0xffff));
        Assert.False(PulsarModelCatalog.Supported(0x1234, 0x5406));
    }

    [Fact]
    public void PulsarRejectsChecksumWrongCommandEchoAndInvalidLevels()
    {
        var bad = Frame(10, false); bad[9] ^= 1;
        Assert.False(PulsarProtocol.TryParse(bad, out _, out _));
        var wrongCommand = Frame(10, false); wrongCommand[1] = 0x0a;
        wrongCommand[16] = PulsarProtocol.Checksum(wrongCommand.AsSpan(0, 16));
        Assert.False(PulsarProtocol.TryParse(wrongCommand, out _, out _));
        Assert.False(PulsarProtocol.TryParse(PulsarProtocol.PowerRequest(), out _, out _));
        Assert.False(PulsarProtocol.TryParse(Frame(101, false), out _, out _));
        Assert.False(PulsarProtocol.TryParse(new byte[5], out _, out _));
    }

    [Theory]
    [InlineData("BATTERY_AVAILABLE", 9, true, false)]
    [InlineData("BATTERY_CHARGING", 10, true, true)]
    [InlineData("BATTERY_UNAVAILABLE", 0, false, null)]
    [InlineData("BATTERY_AVAILABLE", 101, false, false)]
    public void HeadsetStatusesDoNotInferZeroWhenUnavailable(string status, int level, bool valid, bool? charging)
    {
        var json = JsonSerializer.Serialize(new { devices = new[] { new { device = "PRO X 2", id_vendor = "0x046d", id_product = "0x0af7", battery = new { status, level } } } });
        var reading = Assert.Single(HeadsetJson.Parse(json, DateTimeOffset.UtcNow));
        Assert.Equal(valid, reading.IsValid); Assert.Equal(charging, reading.Charging);
        if (!valid) Assert.Null(reading.Percent);
    }

    [Fact]
    public void HeadsetInvalidJsonIsRejectedAndMultipleDevicesRemainSeparate()
    {
        Assert.ThrowsAny<JsonException>(() => HeadsetJson.Parse("invalid", DateTimeOffset.UtcNow));
        Assert.Throws<JsonException>(() => HeadsetJson.Parse("{}", DateTimeOffset.UtcNow));
        var readings = HeadsetJson.Parse("""
        {"devices":[{"device":"A","id_vendor":"0x1","id_product":"0x2"},
                    {"device":"B","id_vendor":"0x1","id_product":"0x3"}]}
        """, DateTimeOffset.UtcNow);
        Assert.Equal(2, readings.Count);
        Assert.NotEqual(readings[0].Device.Id, readings[1].Device.Id);
        Assert.All(readings, reading => Assert.Equal(Availability.Unsupported, reading.Availability));
    }
}
