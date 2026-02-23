namespace SAED_PortalEmpleado.Domain.Entities;

public class Employee
{
    public Guid Id { get; set; }
    public string GoogleSub { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? Cuil { get; set; }
    public string? PictureUrl { get; set; }
    public DateTime CreatedAt { get; set; }
}
