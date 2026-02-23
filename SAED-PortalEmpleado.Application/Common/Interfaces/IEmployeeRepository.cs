using SAED_PortalEmpleado.Domain.Entities;

namespace SAED_PortalEmpleado.Application.Common.Interfaces;

/// <summary>
/// Repository interface for Employee operations
/// </summary>
public interface IEmployeeRepository
{
    Task<Employee?> GetByGoogleSubAsync(string googleSub, CancellationToken cancellationToken = default);
    Task<Employee?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<Employee?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<Employee>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Employee> AddAsync(Employee employee, CancellationToken cancellationToken = default);
    Task UpdateAsync(Employee employee, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
