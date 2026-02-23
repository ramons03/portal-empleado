using SAED_PortalEmpleado.Application.Common.Interfaces;

namespace SAED_PortalEmpleado.Api.Services;

public class DateTimeProvider : IDateTimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
    public DateTime Now => DateTime.Now;
}
