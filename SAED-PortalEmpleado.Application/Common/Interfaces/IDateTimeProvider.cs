namespace SAED_PortalEmpleado.Application.Common.Interfaces;

/// <summary>
/// Provides access to current date and time
/// Abstraction for better testability
/// </summary>
public interface IDateTimeProvider
{
    DateTime UtcNow { get; }
    DateTime Now { get; }
}
