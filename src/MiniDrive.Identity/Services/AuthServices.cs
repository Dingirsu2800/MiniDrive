using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using MiniDrive.Common.Jwt;
using MiniDrive.Identity.DTOs;
using MiniDrive.Identity.Entities;
using MiniDrive.Identity.Repositories;

namespace MiniDrive.Identity.Services;

public class AuthService : IAuthService
{
    private readonly UserRepository _userRepository;
    private readonly TimeSpan _sessionLifetime;
    private readonly JwtTokenGenerator _tokenGenerator;
    private readonly JwtOptions _jwtOptions;
    private readonly JwtSecurityTokenHandler _tokenHandler = new();
    private readonly TokenValidationParameters _tokenValidationParameters;

    public AuthService(
        UserRepository userRepository,
        JwtTokenGenerator tokenGenerator,
        IOptions<JwtOptions> jwtOptions,
        TimeSpan? sessionLifetime = null)
    {
        _userRepository = userRepository;
        _tokenGenerator = tokenGenerator;
        _jwtOptions = jwtOptions.Value;

        if (!_jwtOptions.IsValid(out var error))
        {
            throw new InvalidOperationException($"Invalid JWT configuration: {error}");
        }

        _sessionLifetime = sessionLifetime ?? _jwtOptions.AccessTokenLifetime;

        _tokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.SigningKey)),
            ValidateIssuer = true,
            ValidIssuer = _jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = _jwtOptions.Audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    }

    public async Task<AuthResult> RegisterAsync(
        RegisterRequest request,
        string? userAgent = null,
        string? ipAddress = null)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return AuthResult.Failure("Email and password are required.");
        }

        var email = request.Email.Trim().ToLowerInvariant();
        var existing = await _userRepository.GetByEmailAsync(email);
        if (existing is not null)
        {
            return AuthResult.Failure("Email is already registered.");
        }

        var (hash, salt) = HashPassword(request.Password);
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            DisplayName = string.IsNullOrWhiteSpace(request.DisplayName)
                ? email
                : request.DisplayName.Trim(),
            PasswordHash = hash,
            PasswordSalt = salt,
            CreatedAtUtc = DateTime.UtcNow,
            IsActive = true
        };

        await _userRepository.AddAsync(user);
        var session = await _userRepository.CreateSessionAsync(
            user.Id,
            _sessionLifetime,
            userAgent,
            ipAddress);

        var accessToken = _tokenGenerator.GenerateToken(user, session);
        return AuthResult.Success(user, session, accessToken);
    }

    public async Task<AuthResult> LoginAsync(
        LoginRequest request,
        string? userAgent = null,
        string? ipAddress = null)
    {
        var email = request.Email?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return AuthResult.Failure("Email and password are required.");
        }

        var user = await _userRepository.GetByEmailAsync(email);
        if (user is null || !user.IsActive)
        {
            return AuthResult.Failure("Invalid credentials.");
        }

        if (!VerifyPassword(request.Password, user.PasswordHash, user.PasswordSalt))
        {
            return AuthResult.Failure("Invalid credentials.");
        }

        user.LastLoginAtUtc = DateTime.UtcNow;
        await _userRepository.UpdateAsync(user);

        var session = await _userRepository.CreateSessionAsync(
            user.Id,
            _sessionLifetime,
            userAgent,
            ipAddress);

        var accessToken = _tokenGenerator.GenerateToken(user, session);
        return AuthResult.Success(user, session, accessToken);
    }

    public async Task<bool> LogoutAsync(string jwt)
    {
        var sessionId = GetSessionIdFromToken(jwt);
        if (sessionId is null)
        {
            return false;
        }

        return await _userRepository.RemoveSessionAsync(sessionId);
    }

    public async Task<int> LogoutAllAsync(string jwt)
    {
        Guid? userId = null;

        // First, try to read the JWT payload without full validation so we can
        // resolve the subject (user id) even if the token is near expiry.
        try
        {
            var jwtToken = _tokenHandler.ReadJwtToken(jwt);
            var sub = jwtToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub)?.Value;
            if (!string.IsNullOrWhiteSpace(sub) && Guid.TryParse(sub, out var parsedUserId))
            {
                userId = parsedUserId;
            }
        }
        catch
        {
            // Ignore malformed tokens here; we'll fall back to session lookup.
        }

        // If we still don't have a user id, treat the input as a raw session token.
        if (userId is null)
        {
            var session = await _userRepository.GetSessionAsync(jwt);
            if (session is not null)
            {
                userId = session.UserId;
            }
        }

        if (userId is null)
        {
            return 0;
        }

        return await _userRepository.RemoveSessionsForUserAsync(userId.Value);
    }

    public async Task<User?> ValidateSessionAsync(string token)
    {
        var principal = ValidateJwt(token);
        var sessionId = principal?.FindFirstValue("sid");
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return null;
        }

        var session = await _userRepository.GetSessionAsync(sessionId);
        if (session is null)
        {
            return null;
        }

        var user = await _userRepository.GetByIdAsync(session.UserId);
        return user is not null && user.IsActive ? user : null;
    }

    public Task CleanupAsync() => _userRepository.CleanupExpiredSessionsAsync();

    private ClaimsPrincipal? ValidateJwt(string token)
    {
        try
        {
            return _tokenHandler.ValidateToken(token, _tokenValidationParameters, out _);
        }
        catch
        {
            return null;
        }
    }

    private string? GetSessionIdFromToken(string token) =>
        ValidateJwt(token)?.FindFirstValue("sid");

    private static (string Hash, string Salt) HashPassword(string password)
    {
        var saltBytes = RandomNumberGenerator.GetBytes(16);
        var hashBytes = Rfc2898DeriveBytes.Pbkdf2(
            password,
            saltBytes,
            100_000,
            HashAlgorithmName.SHA256,
            32);

        return (Convert.ToHexString(hashBytes), Convert.ToHexString(saltBytes));
    }

    private static bool VerifyPassword(string password, string storedHash, string storedSalt)
    {
        if (string.IsNullOrWhiteSpace(storedHash) || string.IsNullOrWhiteSpace(storedSalt))
        {
            return false;
        }

        var saltBytes = Convert.FromHexString(storedSalt);
        var computedHashBytes = Rfc2898DeriveBytes.Pbkdf2(
            password,
            saltBytes,
            100_000,
            HashAlgorithmName.SHA256,
            32);

        return CryptographicOperations.FixedTimeEquals(
            computedHashBytes,
            Convert.FromHexString(storedHash));
    }
}

public sealed class AuthResult
{
    public bool Succeeded { get; }
    public string? Error { get; }
    public User? User { get; }
    public SessionInfo? Session { get; }
    public string? AccessToken { get; }

    private AuthResult(bool succeeded, string? error, User? user, SessionInfo? session, string? accessToken)
    {
        Succeeded = succeeded;
        Error = error;
        User = user;
        Session = session;
        AccessToken = accessToken;
    }

    public static AuthResult Failure(string message) => new(false, message, null, null, null);

    public static AuthResult Success(User user, SessionInfo session, string accessToken) =>
        new(true, null, user, session, accessToken);
}
