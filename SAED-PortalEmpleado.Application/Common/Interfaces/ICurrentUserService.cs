namespace SAED_PortalEmpleado.Application.Common.Interfaces;

/// <summary>
/// Service for accessing current authenticated user information
/// </summary>
public interface ICurrentUserService
{
    /// <summary>
    /// Gets the current user's Google Sub (unique identifier)
    /// </summary>
    string? GoogleSub { get; }
    
    /// <summary>
    /// Gets the current user's email
    /// </summary>
    string? Email { get; }
    
    /// <summary>
    /// Gets the current user's full name
    /// </summary>
    string? FullName { get; }
    
    /// <summary>
    /// Checks if a user is authenticated
    /// </summary>
    bool IsAuthenticated { get; }
}
