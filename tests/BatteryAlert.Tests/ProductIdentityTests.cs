using BatteryAlert.Core;
using Xunit;

namespace BatteryAlert.Tests;

public sealed class ProductIdentityTests
{
    [Theory]
    [InlineData("Logitech G PRO X 2 LIGHTSPEED", "HeadsetControl", "logitech-pro-x2.png")]
    [InlineData("Logitech G PRO X Wireless", "HeadsetControl", "logitech-pro-x.png")]
    [InlineData("Pulsar Xlite V3", "Pulsar Fusion", "pulsar-xlite-v3.png")]
    [InlineData("Pulsar Xlite CrazyLight", "Pulsar CrazyLight", "pulsar-xlite-crazylight.png")]
    [InlineData("Powerbeat Pro", "Windows", "beats-powerbeats-pro.jpg")]
    [InlineData("Pulsar Fusion (X2 v2 / X2A / Xlite v3)", "Pulsar Fusion", null)]
    [InlineData("Pulsar Xlite V3 eS", "Pulsar Fusion", null)]
    [InlineData("Powerbeats Pro 2", "Windows", null)]
    [InlineData("Logitech PRO X wired", "HeadsetControl", null)]
    [InlineData("Logitech PRO X Superlight 2 Wireless", "Windows", null)]
    public void ImagesMatchReportedModelsAndDoNotGuessFromSharedReceivers(string name, string provider, string? file)
    {
        var identity = ProductIdentity.Resolve(new("1", name, provider, "3554:F508"));
        Assert.Equal(file, identity.ImageFile);
    }

    [Fact]
    public void ChangingDraftArtworkDoesNotChangeSavedSettings()
    {
        var saved = new AppSettings();
        saved.For("mouse").ProductImageFile = "original.png";
        var draft = saved.Clone();
        draft.For("mouse").ProductImageFile = "replacement.png";
        Assert.Equal("original.png", saved.For("mouse").ProductImageFile);
        Assert.Equal("replacement.png", draft.For("mouse").ProductImageFile);
    }
}
