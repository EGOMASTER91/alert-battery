namespace BatteryAlert.Core;

public sealed class ProviderPoller(TimeSpan? timeout = null)
{
    private readonly TimeSpan _timeout = timeout ?? TimeSpan.FromSeconds(12);
    private readonly System.Collections.Concurrent.ConcurrentDictionary<IBatteryProvider, IReadOnlyList<DeviceDescriptor>> _known = new();
    private readonly System.Collections.Concurrent.ConcurrentDictionary<IBatteryProvider, Task<IReadOnlyList<BatteryReading>>> _running = new();
    public event Action<string>? Error;

    public async Task<IReadOnlyList<BatteryReading>> PollAsync(IEnumerable<IBatteryProvider> providers, CancellationToken ct)
    {
        var batches = await Task.WhenAll(providers.Select(provider => PollBoundedAsync(provider, ct)));
        return batches.SelectMany(x => x).ToArray();
    }

    private async Task<IReadOnlyList<BatteryReading>> PollBoundedAsync(IBatteryProvider provider, CancellationToken ct)
    {
        // A provider which ignores cancellation may finish later. Never start overlapping HID work.
        if (_running.TryGetValue(provider, out var previous) && !previous.IsCompleted)
            return Missing(provider, "Cititorul nu a răspuns încă");
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(_timeout);
        var task = PollProviderAsync(provider, deadline.Token);
        _running[provider] = task;
        try { return await task.WaitAsync(_timeout, ct); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            Error?.Invoke($"{provider.Name}: timpul de citire a expirat.");
            return Missing(provider, "Citirea a expirat");
        }
        catch (TimeoutException)
        {
            Error?.Invoke($"{provider.Name}: timpul de citire a expirat.");
            return Missing(provider, "Citirea a expirat");
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            Error?.Invoke($"{provider.Name}: {ex.Message}");
            return Missing(provider, "Cititorul a întâmpinat o eroare");
        }
        finally
        {
            deadline.Cancel();
            // Observe errors even when the outer timeout abandoned a misbehaving provider.
            _ = task.ContinueWith(t => _ = t.Exception, CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
        }
    }

    private IReadOnlyList<BatteryReading> Missing(IBatteryProvider provider, string detail) =>
        _known.GetValueOrDefault(provider, []).Select(d => BatteryReading.Unavailable(d, detail)).ToArray();

    private async Task<IReadOnlyList<BatteryReading>> PollProviderAsync(IBatteryProvider provider, CancellationToken ct)
    {
        var devices = await provider.DiscoverAsync(ct);
        _known[provider] = devices;
        var readings = new List<BatteryReading>();
        foreach (var device in devices)
        {
            ct.ThrowIfCancellationRequested();
            try { readings.Add(await provider.ReadAsync(device, ct)); }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                Error?.Invoke($"{provider.Name}: {device.Name}: {ex.Message}");
                readings.Add(BatteryReading.Unavailable(device, "Citirea a eșuat"));
            }
        }
        return readings;
    }
}
