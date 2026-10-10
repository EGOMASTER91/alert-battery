using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace BatteryAlert.Core;

public sealed partial class NtfyClient(HttpClient http)
{
    [GeneratedRegex("^[A-Za-z0-9_-]{1,64}$")]
    private static partial Regex TopicPattern();
    public static bool IsValidTopic(string topic) => !string.IsNullOrWhiteSpace(topic) && TopicPattern().IsMatch(topic);
    public static bool IsValidServer(string server) => Uri.TryCreate(server, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps && string.IsNullOrEmpty(uri.UserInfo)
        && string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.Fragment);

    public static Uri TopicUri(AppSettings settings)
    {
        if (!IsValidServer(settings.NtfyServer) || !IsValidTopic(settings.NtfyTopic))
            throw new ArgumentException("Serverul trebuie să folosească HTTPS, iar canalul litere, cifre, - sau _.");
        return new(settings.NtfyServer.TrimEnd('/') + "/" + settings.NtfyTopic);
    }

    public async Task SendAsync(AppSettings settings, string message, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, TopicUri(settings));
        request.Content = new StringContent(message, Encoding.UTF8, "text/plain");
        request.Headers.TryAddWithoutValidation("Title", "Battery Alert");
        request.Headers.TryAddWithoutValidation("Tags", "battery");
        request.Headers.TryAddWithoutValidation("Priority", "4");
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!response.IsSuccessStatusCode)
            throw new NtfyDeliveryException(response.StatusCode, response.Headers.RetryAfter?.Delta);
    }
}

public sealed class NtfyDeliveryException(HttpStatusCode statusCode, TimeSpan? retryAfter = null)
    : Exception($"ntfy: HTTP {(int)statusCode}")
{
    public HttpStatusCode StatusCode { get; } = statusCode;
    public TimeSpan? RetryAfter { get; } = retryAfter;
    public bool Permanent => (int)StatusCode is >= 400 and < 500 && StatusCode is not HttpStatusCode.TooManyRequests
        and not HttpStatusCode.RequestTimeout;
}

public static class NotificationQueue
{
    public static bool Relevant(PendingNotification item, BatteryReading? latest,
        DeviceSettings settings, DateTimeOffset now) =>
        settings.Enabled && settings.Phone && now - item.Alert.CreatedAt < TimeSpan.FromMinutes(15)
        && latest is { IsValid: true, Charging: not true }
        && now - latest.ReadAt <= TimeSpan.FromMinutes(2) && latest.Percent <= settings.Threshold;

    public static bool ScheduleRetry(PendingNotification item, DateTimeOffset now, TimeSpan? retryAfter = null)
    {
        item.Attempts++;
        if (item.Attempts >= 5) return false;
        var delay = retryAfter ?? TimeSpan.FromSeconds(Math.Pow(2, item.Attempts - 1) * 30);
        delay = TimeSpan.FromSeconds(Math.Clamp(delay.TotalSeconds, 10, 300));
        item.NextAttempt = now + delay;
        return item.NextAttempt - item.Alert.CreatedAt < TimeSpan.FromMinutes(15);
    }
}
