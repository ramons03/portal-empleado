using System.Collections.Concurrent;
using SAED_PortalEmpleado.Application.Common.Interfaces;

namespace SAED_PortalEmpleado.Infrastructure.Services;

public sealed class ReceiptPeriodLock : IReceiptPeriodLock
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new(StringComparer.Ordinal);

    public async Task<IAsyncDisposable> AcquireAsync(
        string cuil,
        int year,
        int month,
        CancellationToken cancellationToken = default)
    {
        var key = BuildKey(cuil, year, month);
        var semaphore = _locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync(cancellationToken);
        return new Releaser(semaphore);
    }

    private static string BuildKey(string cuil, int year, int month)
    {
        var normalizedCuil = new string(cuil.Where(char.IsDigit).ToArray());
        return $"{normalizedCuil}:{year:D4}-{month:D2}";
    }

    private sealed class Releaser : IAsyncDisposable
    {
        private readonly SemaphoreSlim _semaphore;
        private bool _released;

        public Releaser(SemaphoreSlim semaphore)
        {
            _semaphore = semaphore;
        }

        public ValueTask DisposeAsync()
        {
            if (_released)
            {
                return ValueTask.CompletedTask;
            }

            _semaphore.Release();
            _released = true;
            return ValueTask.CompletedTask;
        }
    }
}
