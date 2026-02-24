namespace SAED_PortalEmpleado.Application.Common.Interfaces;

public interface IReceiptPeriodLock
{
    Task<IAsyncDisposable> AcquireAsync(
        string cuil,
        int year,
        int month,
        CancellationToken cancellationToken = default);
}
