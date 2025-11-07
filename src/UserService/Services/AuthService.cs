using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using UserService.Data;
using UserService.DTOs;
using UserService.Models;

namespace UserService.Services;

public interface IAuthService
{
    Task<UserResponse?> RegisterAsync(RegisterRequest request);
    Task<LoginResponse?> LoginAsync(LoginRequest request);
    Task<UserResponse?> GetUserByIdAsync(Guid userId);
}

public class AuthService : IAuthService
{
    private readonly UserDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        UserDbContext context,
        IConfiguration configuration,
        ILogger<AuthService> logger)
    {
        _context = context;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<UserResponse?> RegisterAsync(RegisterRequest request)
    {
        try
        {
            // Check if username or email already exists
            if (await _context.Users.AnyAsync(u => u.Username == request.Username))
            {
                _logger.LogWarning("Username {Username} already exists", request.Username);
                return null;
            }

            if (await _context.Users.AnyAsync(u => u.Email == request.Email))
            {
                _logger.LogWarning("Email {Email} already exists", request.Email);
                return null;
            }

            // Hash password
            var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);

            var user = new User
            {
                Username = request.Username,
                Email = request.Email,
                PasswordHash = passwordHash,
                FirstName = request.FirstName,
                LastName = request.LastName,
                PhoneNumber = request.PhoneNumber,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            _logger.LogInformation("User {Username} registered successfully", user.Username);

            return MapToUserResponse(user);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registering user");
            return null;
        }
    }

    public async Task<LoginResponse?> LoginAsync(LoginRequest request)
    {
        try
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Username == request.Username);

            if (user == null)
            {
                _logger.LogWarning("User {Username} not found", request.Username);
                return null;
            }

            if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            {
                _logger.LogWarning("Invalid password for user {Username}", request.Username);
                return null;
            }

            if (!user.IsActive)
            {
                _logger.LogWarning("User {Username} is not active", request.Username);
                return null;
            }

            var token = GenerateJwtToken(user);

            return new LoginResponse
            {
                Token = token,
                User = MapToUserResponse(user)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error logging in user");
            return null;
        }
    }

    public async Task<UserResponse?> GetUserByIdAsync(Guid userId)
    {
        try
        {
            var user = await _context.Users.FindAsync(userId);
            return user != null ? MapToUserResponse(user) : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user by id");
            return null;
        }
    }

    private string GenerateJwtToken(User user)
    {
        var jwtSettings = _configuration.GetSection("JwtSettings");
        var secretKey = jwtSettings["SecretKey"] ?? "YourDefaultSecretKeyForDevelopment123!";
        var issuer = jwtSettings["Issuer"] ?? "UserService";
        var audience = jwtSettings["Audience"] ?? "BookingSystem";
        var expirationMinutes = int.Parse(jwtSettings["ExpirationMinutes"] ?? "60");

        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.UniqueName, user.Username),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expirationMinutes),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static UserResponse MapToUserResponse(User user)
    {
        return new UserResponse
        {
            Id = user.Id,
            Username = user.Username,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            PhoneNumber = user.PhoneNumber,
            IsActive = user.IsActive,
            CreatedAt = user.CreatedAt
        };
    }
}
