using ECommerce.Users.Application.DTOs.Auth;
using ECommerce.Shared.Exceptions;
using ECommerce.Users.Application.Interfaces;
using ECommerce.Shared.Roles;
using ECommerce.Users.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using ECommerce.Shared.Data;

namespace ECommerce.Users.Application.Services;

public class AuthService
{
    private readonly IUserRepository _userRepository;
    private readonly IRoleRepository _roleRepository;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly ITokenService _tokenService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IUserRepository userRepository,
        IRoleRepository roleRepository,
        IPasswordHasher<User> passwordHasher,
        ITokenService tokenService,
        IUnitOfWork unitOfWork,
        ILogger<AuthService> logger)
    {
        _userRepository = userRepository;
        _roleRepository = roleRepository;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        var email = NormalizeEmail(request.Email);

        if (await _userRepository.EmailExistsAsync(email, cancellationToken))
        {
            throw new ConflictException($"An account with email '{email}' already exists.");
        }

        var role = await _roleRepository.GetByNameAsync(RoleNames.Customer, cancellationToken)
            ?? throw new InvalidOperationException(
                $"Role '{RoleNames.Customer}' is missing. The database has not been seeded.");

        var now = DateTime.UtcNow;

        var user = new User
        {
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            Email = email,
            RoleId = role.Id,
            Role = role,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };

        user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);

        _userRepository.Add(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("User {UserId} registered with email {Email}", user.Id, user.Email);

        var token = _tokenService.CreateToken(user);

        return new AuthResponse(token.Token, token.ExpiresAt, ToResponse(user));
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var email = NormalizeEmail(request.Email);
        var user = await _userRepository.GetByEmailAsync(email, cancellationToken);

        if (user is null)
        {
            _logger.LogWarning("Failed login attempt for {Email}: no such account", email);
            throw new UnauthorizedException("Invalid email or password.");
        }

        var verification = VerifyPassword(user, request.Password);

        if (verification == PasswordVerificationResult.Failed)
        {
            _logger.LogWarning("Failed login attempt for {Email}: wrong password", email);
            throw new UnauthorizedException("Invalid email or password.");
        }

        if (!user.IsActive)
        {
            _logger.LogWarning("Failed login attempt for {Email}: account deactivated", email);
            throw new UnauthorizedException("Invalid email or password.");
        }

        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);
            user.UpdatedAt = DateTime.UtcNow;

            _userRepository.Update(user);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Rehashed stored password for user {UserId}", user.Id);
        }

        var token = _tokenService.CreateToken(user);

        _logger.LogInformation("User {UserId} logged in", user.Id);

        return new AuthResponse(token.Token, token.ExpiresAt, ToResponse(user));
    }

    public async Task<UserResponse> GetCurrentUserAsync(int userId, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException($"User with id {userId} was not found.");

        return ToResponse(user);
    }

    private PasswordVerificationResult VerifyPassword(User user, string password)
    {
        if (string.IsNullOrEmpty(user.PasswordHash))
        {
            return PasswordVerificationResult.Failed;
        }

        return _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);
    }

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    private static UserResponse ToResponse(User user) => new(
        user.Id,
        user.FirstName,
        user.LastName,
        user.Email,
        user.Role.Name,
        user.IsActive,
        user.CreatedAt);
}
