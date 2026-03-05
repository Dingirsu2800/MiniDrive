using MiniDrive.Identity.DTOs;
using MiniDrive.Identity.Entities;

namespace MiniDrive.Identity.Services;

/// <summary>
/// Interface for authentication services (register, login, logout).
/// </summary>
public interface IAuthServices
{
    /// <summary>
    /// Registers a new user.
    /// </summary>
    Task<AuthResult> RegisterAsync(
        RegisterRequest request,
        string? userAgent = null,
        string? ipAddress = null);

    /// <summary>
    /// Authenticates a user and creates a session.
    /// </summary>
    Task<AuthResult> LoginAsync(
        LoginRequest request,
        string? userAgent = null,
        string? ipAddress = null);

    /// <summary>
    /// Logs out a user by invalidating their session.
    /// </summary>
    Task<bool> LogoutAsync(string jwt);

    /// <summary>
    /// Logs out a user from all devices.
    /// </summary>
    Task<int> LogoutAllAsync(string jwt);

    /// <summary>
    /// Validates a session token and returns the user.
    /// </summary>
    Task<User?> ValidateSessionAsync(string token);

    /// <summary>
    /// Cleans up expired sessions.
    /// </summary>
    Task CleanupAsync();
}
