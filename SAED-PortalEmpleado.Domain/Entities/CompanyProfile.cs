namespace SAED_PortalEmpleado.Domain.Entities;

public class CompanyProfile
{
    public Guid Id { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string Cuit { get; set; } = string.Empty;
    public string? AddressLine { get; set; }
    public string? City { get; set; }
    public string? Province { get; set; }
    public string? PostalCode { get; set; }
    public string? Country { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
}
