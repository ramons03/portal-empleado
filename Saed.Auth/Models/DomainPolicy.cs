namespace Saed.Auth.Models;

public enum DomainPolicyType
{
    Strict,
    Edu,
    Public
}

public class DomainPolicy
{
    public List<string> AllowedDomains { get; set; } = new();
}
