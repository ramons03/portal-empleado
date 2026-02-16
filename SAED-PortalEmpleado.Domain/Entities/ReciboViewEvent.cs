namespace SAED_PortalEmpleado.Domain.Entities;

public class ReciboViewEvent
{
    public Guid Id { get; set; }
    public string GoogleSub { get; set; } = string.Empty;
    public string? Cuil { get; set; }
    public string Action { get; set; } = "page";
    public string? ReciboId { get; set; }
    public DateTime ViewedAtUtc { get; set; }
}
