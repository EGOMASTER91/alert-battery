using System.Net;
using BatteryAlert.Core;
using Xunit;

namespace BatteryAlert.Tests;

public sealed class NtfyResponseSecurityTests
{
    [Fact]
    public async Task DeliveryDoesNotBufferUntrustedResponseBody()
    {
        using var content = new UnreadableResponse();
        using var client = new HttpClient(new AcceptedHandler(content));
        await new NtfyClient(client).SendAsync(new() { NtfyTopic = "test-only" }, "test", CancellationToken.None);
        Assert.False(content.ReadAttempted);
    }

    private sealed class AcceptedHandler(HttpContent content) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
    }

    private sealed class UnreadableResponse : HttpContent
    {
        public bool ReadAttempted { get; private set; }
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        {
            ReadAttempted = true;
            throw new IOException("The response body must not be buffered.");
        }
        protected override bool TryComputeLength(out long length) { length = 0; return false; }
    }
}
