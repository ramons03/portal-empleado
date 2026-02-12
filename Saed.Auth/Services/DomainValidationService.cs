using Microsoft.Extensions.Options;
using Saed.Auth.Models;

namespace Saed.Auth.Services;

public interface IDomainValidationService
{
    bool ValidateEmailDomain(string email, DomainPolicyType policyType);
}

public class DomainValidationService : IDomainValidationService
{
    private readonly Dictionary<DomainPolicyType, DomainPolicy> _policies;

    public DomainValidationService(IOptions<Dictionary<string, DomainPolicy>> options)
    {
        _policies = new Dictionary<DomainPolicyType, DomainPolicy>();
        var policiesConfig = options.Value;
        
        if (policiesConfig.ContainsKey("Strict"))
            _policies[DomainPolicyType.Strict] = policiesConfig["Strict"];
        if (policiesConfig.ContainsKey("Edu"))
            _policies[DomainPolicyType.Edu] = policiesConfig["Edu"];
        if (policiesConfig.ContainsKey("Public"))
            _policies[DomainPolicyType.Public] = policiesConfig["Public"];
    }

    public bool ValidateEmailDomain(string email, DomainPolicyType policyType)
    {
        if (string.IsNullOrEmpty(email) || !email.Contains("@"))
            return false;

        var domain = email.Split('@')[1].ToLower();
        
        if (!_policies.ContainsKey(policyType))
            return false;

        var allowedDomains = _policies[policyType].AllowedDomains;

        // Public policy allows all domains
        if (allowedDomains.Contains("*"))
            return true;

        // Check for exact match or wildcard match
        foreach (var allowedDomain in allowedDomains)
        {
            if (allowedDomain.StartsWith("*."))
            {
                // Wildcard match (e.g., *.edu)
                var domainSuffix = allowedDomain.Substring(1); // Remove the *
                if (domain.EndsWith(domainSuffix))
                    return true;
            }
            else if (domain.Equals(allowedDomain, StringComparison.OrdinalIgnoreCase))
            {
                // Exact match
                return true;
            }
        }

        return false;
    }
}
