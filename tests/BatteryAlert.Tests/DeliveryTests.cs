using BatteryAlert.Core;
using System.Net;
using Xunit;

namespace BatteryAlert.Tests;

public sealed class DeliveryTests
{
    [Fact]
    public async Task NtfyUsesHttpsUtf8AndLowBatteryMetadata()
    {
        Uri? url = null; string? body = null;
        using var client = new HttpClient(new FakeHandler(async request =>
        {
            url = request.RequestUri; body = await request.Content!.ReadAsStringAsync();
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("Battery Alert", Assert.Single(request.Headers.GetValues("Title")));
            return new(HttpStatusCode.OK);
        }));
        await new NtfyClient(client).SendAsync(new() { NtfyTopic = "test-topic" }, "Căști — baterie 9%", CancellationToken.None);
        Assert.Equal("https://ntfy.sh/test-topic", url!.AbsoluteUri); Assert.Equal("Căști — baterie 9%", body);
    }

    [Theory]
    [InlineData("http://ntfy.sh", "valid", false)] [InlineData("https://ntfy.sh", "../bad", false)]
    [InlineData("https://host/path", "valid-topic_1", true)] [InlineData("https://user:pass@host", "valid", false)]
    public void ValidatesDestination(string server, string topic, bool expected)
    { Assert.Equal(expected, NtfyClient.IsValidServer(server) && NtfyClient.IsValidTopic(topic)); }

    [Fact]
    public async Task HttpFailureIsDistinguishedFromAcceptedDelivery()
    {
        using var client = new HttpClient(new FakeHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.TooManyRequests))));
        var error = await Assert.ThrowsAsync<NtfyDeliveryException>(() => new NtfyClient(client).SendAsync(new(), "test", CancellationToken.None));
        Assert.False(error.Permanent); Assert.Equal(HttpStatusCode.TooManyRequests, error.StatusCode);
    }

    [Fact]
    public void QueueExpiresAndDropsChargedDisconnectedOrDisabledDevices()
    {
        var now = DateTimeOffset.UtcNow; var device = new DeviceDescriptor("1", "Mouse", "test");
        var item = new PendingNotification { Alert = new("id", device.Id, device.Name, 9, now.AddMinutes(-1)) };
        var reading = new BatteryReading(device, 9, false, Availability.Available, now);
        Assert.True(NotificationQueue.Relevant(item, reading, new(), now));
        Assert.False(NotificationQueue.Relevant(item, reading with { Charging = true }, new(), now));
        Assert.False(NotificationQueue.Relevant(item, reading with { Percent = 20 }, new(), now));
        Assert.False(NotificationQueue.Relevant(item, null, new(), now));
        Assert.False(NotificationQueue.Relevant(item, reading, new() { Phone = false }, now));
        Assert.False(NotificationQueue.Relevant(item, reading, new(), now.AddMinutes(15)));
        for (var i = 0; i < 4; i++) Assert.True(NotificationQueue.ScheduleRetry(item, now));
        Assert.False(NotificationQueue.ScheduleRetry(item, now));
    }

    [Fact]
    public async Task ReconnectionRecoversAndAnUncancelableReadNeverOverlaps()
    {
        var provider = new ReconnectingProvider();
        var poller = new ProviderPoller(TimeSpan.FromMilliseconds(40));
        var initial = await poller.PollAsync([provider], CancellationToken.None);
        Assert.False(Assert.Single(initial).IsValid);
        await poller.PollAsync([provider], CancellationToken.None);
        Assert.Equal(1, provider.Calls);
        provider.Complete();
        await provider.FirstReadCompleted;
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        IReadOnlyList<BatteryReading> reconnected;
        do
        {
            await Task.Delay(5, deadline.Token);
            reconnected = await poller.PollAsync([provider], deadline.Token);
        } while (provider.Calls < 2);
        Assert.Equal(9, Assert.Single(reconnected).Percent);
        Assert.Equal(2, provider.Calls);
    }

    private sealed class ReconnectingProvider : IBatteryProvider
    {
        private readonly TaskCompletionSource<BatteryReading> _first = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private static readonly DeviceDescriptor Device = new("reconnect", "Mouse", "Test");
        public string Name => "Reconnect";
        public int Calls { get; private set; }
        public Task FirstReadCompleted { get; private set; } = Task.CompletedTask;
        public Task<IReadOnlyList<DeviceDescriptor>> DiscoverAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<DeviceDescriptor>>([Device]);
        public Task<BatteryReading> ReadAsync(DeviceDescriptor device, CancellationToken ct)
        {
            Calls++;
            if (Calls != 1) return Task.FromResult(new BatteryReading(device, 9, false, Availability.Available, DateTimeOffset.UtcNow));
            FirstReadCompleted = _first.Task;
            return _first.Task;
        }
        public void Complete() => _first.SetResult(BatteryReading.Unavailable(Device));
    }

    [Fact]
    public async Task FailedAndStuckProvidersDoNotBlockHealthyDevices()
    {
        var poller = new ProviderPoller(TimeSpan.FromMilliseconds(100));
        var errors = new System.Collections.Concurrent.ConcurrentBag<string>(); poller.Error += errors.Add;
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var result = await poller.PollAsync(new[] { new FakeProvider("healthy"), new FakeProvider("failed", fail: true), new FakeProvider("stuck", stuck: true) }, stop.Token);
        Assert.Equal("healthy", Assert.Single(result).Device.Name);
        Assert.Equal(2, errors.Count);
    }

    private sealed class FakeHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => response(request);
    }
    private sealed class FakeProvider(string name, bool fail = false, bool stuck = false) : IBatteryProvider
    {
        public string Name => name;
        public async Task<IReadOnlyList<DeviceDescriptor>> DiscoverAsync(CancellationToken ct)
        {
            if (fail) throw new IOException("failure");
            if (stuck) await Task.Delay(Timeout.Infinite, ct);
            return [new(name, name, "test")];
        }
        public Task<BatteryReading> ReadAsync(DeviceDescriptor device, CancellationToken ct) =>
            Task.FromResult(new BatteryReading(device, 9, false, Availability.Available, DateTimeOffset.UtcNow));
    }
}
