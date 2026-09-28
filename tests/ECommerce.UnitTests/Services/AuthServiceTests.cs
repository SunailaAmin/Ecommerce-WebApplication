using ECommerce.Application.DTOs.Auth;
using ECommerce.Application.Exceptions;
using ECommerce.Application.Interfaces;
using ECommerce.Application.Services;
using ECommerce.Domain;
using ECommerce.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ECommerce.UnitTests.Services;

public class AuthServiceTests
{
    private readonly Mock<IUserRepository> _userRepository = new();
    private readonly Mock<IRoleRepository> _roleRepository = new();
    private readonly Mock<ITokenService> _tokenService = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly PasswordHasher<User> _passwordHasher = new();
    private readonly AuthService _service;

    public AuthServiceTests()
    {
        _service = new AuthService(
            _userRepository.Object,
            _roleRepository.Object,
            _passwordHasher,
            _tokenService.Object,
            _unitOfWork.Object,
            NullLogger<AuthService>.Instance);

        _roleRepository
            .Setup(x => x.GetByNameAsync(RoleNames.Customer, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Role { Id = 1, Name = RoleNames.Customer });

        _tokenService
            .Setup(x => x.CreateToken(It.IsAny<User>()))
            .Returns(new AccessToken("token-value", DateTime.UtcNow.AddHours(1)));
    }

    private static RegisterRequest ValidRegisterRequest() => new()
    {
        FirstName = "  Sara  ",
        LastName = "  Khan  ",
        Email = "  Sara.Khan@Example.COM ",
        Password = "Passw0rd!"
    };

    private User CreateUser(string email = "sara@example.com", bool isActive = true)
    {
        var user = new User
        {
            Id = 7,
            FirstName = "Sara",
            LastName = "Khan",
            Email = email,
            RoleId = 1,
            Role = new Role { Id = 1, Name = RoleNames.Customer },
            IsActive = isActive,
            CreatedAt = DateTime.UtcNow
        };

        user.PasswordHash = _passwordHasher.HashPassword(user, "Passw0rd!");

        return user;
    }

    [Fact]
    public async Task RegisterAsync_CreatesUserWithCustomerRole()
    {
        _userRepository
            .Setup(x => x.EmailExistsAsync("sara.khan@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        User? captured = null;
        _userRepository
            .Setup(x => x.Add(It.IsAny<User>()))
            .Callback<User>(u => captured = u);

        var result = await _service.RegisterAsync(ValidRegisterRequest(), CancellationToken.None);

        Assert.NotNull(captured);
        Assert.Equal("sara.khan@example.com", captured!.Email);
        Assert.Equal("Sara", captured.FirstName);
        Assert.Equal("Khan", captured.LastName);
        Assert.Equal(1, captured.RoleId);
        Assert.True(captured.IsActive);
        Assert.Equal("token-value", result.Token);
        Assert.Equal(RoleNames.Customer, result.User.Role);
        _unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RegisterAsync_StoresHashedPassword_NotPlainText()
    {
        _userRepository
            .Setup(x => x.EmailExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        User? captured = null;
        _userRepository
            .Setup(x => x.Add(It.IsAny<User>()))
            .Callback<User>(u => captured = u);

        await _service.RegisterAsync(ValidRegisterRequest(), CancellationToken.None);

        Assert.NotNull(captured);
        Assert.NotEqual("Passw0rd!", captured!.PasswordHash);
        Assert.NotEmpty(captured.PasswordHash);
    }

    [Fact]
    public async Task RegisterAsync_StoredHashVerifies_WithBuiltInHasher()
    {
        _userRepository
            .Setup(x => x.EmailExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        User? captured = null;
        _userRepository
            .Setup(x => x.Add(It.IsAny<User>()))
            .Callback<User>(u => captured = u);

        await _service.RegisterAsync(ValidRegisterRequest(), CancellationToken.None);

        var result = _passwordHasher.VerifyHashedPassword(captured!, captured!.PasswordHash, "Passw0rd!");

        Assert.NotEqual(PasswordVerificationResult.Failed, result);
    }

    [Fact]
    public async Task RegisterAsync_StoredHashRejectsWrongPassword()
    {
        _userRepository
            .Setup(x => x.EmailExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        User? captured = null;
        _userRepository
            .Setup(x => x.Add(It.IsAny<User>()))
            .Callback<User>(u => captured = u);

        await _service.RegisterAsync(ValidRegisterRequest(), CancellationToken.None);

        var result = _passwordHasher.VerifyHashedPassword(captured!, captured!.PasswordHash, "WrongPass1");

        Assert.Equal(PasswordVerificationResult.Failed, result);
    }

    [Fact]
    public async Task RegisterAsync_StoredHashIsSalted_SoSamePasswordHashesDiffer()
    {
        _userRepository
            .Setup(x => x.EmailExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var hashes = new List<string>();

        _userRepository
            .Setup(x => x.Add(It.IsAny<User>()))
            .Callback<User>(u => hashes.Add(u.PasswordHash));

        await _service.RegisterAsync(ValidRegisterRequest(), CancellationToken.None);
        await _service.RegisterAsync(ValidRegisterRequest(), CancellationToken.None);

        Assert.Equal(2, hashes.Count);
        Assert.NotEqual(hashes[0], hashes[1]);
    }

    [Fact]
    public async Task RegisterAsync_ThrowsConflict_WhenEmailAlreadyExists()
    {
        _userRepository
            .Setup(x => x.EmailExistsAsync("sara.khan@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await Assert.ThrowsAsync<ConflictException>(
            () => _service.RegisterAsync(ValidRegisterRequest(), CancellationToken.None));

        _userRepository.Verify(x => x.Add(It.IsAny<User>()), Times.Never);
        _unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RegisterAsync_Throws_WhenCustomerRoleIsMissing()
    {
        _roleRepository
            .Setup(x => x.GetByNameAsync(RoleNames.Customer, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Role?)null);

        _userRepository
            .Setup(x => x.EmailExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.RegisterAsync(ValidRegisterRequest(), CancellationToken.None));
    }

    [Fact]
    public async Task LoginAsync_ReturnsToken_WhenCredentialsAreValid()
    {
        var user = CreateUser();
        _userRepository
            .Setup(x => x.GetByEmailAsync("sara@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var result = await _service.LoginAsync(
            new LoginRequest { Email = "  SARA@EXAMPLE.COM ", Password = "Passw0rd!" },
            CancellationToken.None);

        Assert.Equal("token-value", result.Token);
        Assert.Equal(user.Id, result.User.Id);
        Assert.Equal(RoleNames.Customer, result.User.Role);
    }

    [Fact]
    public async Task LoginAsync_ThrowsUnauthorized_WhenEmailIsUnknown()
    {
        _userRepository
            .Setup(x => x.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var exception = await Assert.ThrowsAsync<UnauthorizedException>(
            () => _service.LoginAsync(
                new LoginRequest { Email = "nobody@example.com", Password = "Passw0rd!" },
                CancellationToken.None));

        Assert.Equal("Invalid email or password.", exception.Message);
    }

    [Fact]
    public async Task LoginAsync_ThrowsUnauthorized_WhenPasswordIsWrong()
    {
        _userRepository
            .Setup(x => x.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateUser());

        await Assert.ThrowsAsync<UnauthorizedException>(
            () => _service.LoginAsync(
                new LoginRequest { Email = "sara@example.com", Password = "WrongPass1" },
                CancellationToken.None));
    }

    [Fact]
    public async Task LoginAsync_ThrowsUnauthorized_WhenAccountIsDeactivated()
    {
        _userRepository
            .Setup(x => x.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateUser(isActive: false));

        var exception = await Assert.ThrowsAsync<UnauthorizedException>(
            () => _service.LoginAsync(
                new LoginRequest { Email = "sara@example.com", Password = "Passw0rd!" },
                CancellationToken.None));

        Assert.Equal("Invalid email or password.", exception.Message);
    }

    [Fact]
    public async Task LoginAsync_ThrowsUnauthorized_WhenStoredHashIsEmpty()
    {
        var user = CreateUser();
        user.PasswordHash = string.Empty;

        _userRepository
            .Setup(x => x.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        await Assert.ThrowsAsync<UnauthorizedException>(
            () => _service.LoginAsync(
                new LoginRequest { Email = "sara@example.com", Password = "Passw0rd!" },
                CancellationToken.None));
    }

    [Fact]
    public async Task LoginAsync_DoesNotRewriteHash_WhenParametersAreCurrent()
    {
        var user = CreateUser();
        _userRepository
            .Setup(x => x.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        await _service.LoginAsync(
            new LoginRequest { Email = "sara@example.com", Password = "Passw0rd!" },
            CancellationToken.None);

        _userRepository.Verify(x => x.Update(It.IsAny<User>()), Times.Never);
        _unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task LoginAsync_RehashesAndSaves_WhenStoredHashUsesOutdatedParameters()
    {
        var user = CreateUser();
        _userRepository
            .Setup(x => x.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var strictHasher = new PasswordHasher<User>(
            Microsoft.Extensions.Options.Options.Create(new PasswordHasherOptions { IterationCount = 120_000 }));

        var service = new AuthService(
            _userRepository.Object,
            _roleRepository.Object,
            strictHasher,
            _tokenService.Object,
            _unitOfWork.Object,
            NullLogger<AuthService>.Instance);

        var originalHash = user.PasswordHash;

        var result = await service.LoginAsync(
            new LoginRequest { Email = "sara@example.com", Password = "Passw0rd!" },
            CancellationToken.None);

        Assert.False(string.IsNullOrWhiteSpace(result.Token));
        Assert.NotEqual(originalHash, user.PasswordHash);

        var verification = strictHasher.VerifyHashedPassword(user, user.PasswordHash, "Passw0rd!");
        Assert.NotEqual(PasswordVerificationResult.SuccessRehashNeeded, verification);

        _userRepository.Verify(x => x.Update(user), Times.Once);
        _unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task LoginAsync_DoesNotRehash_WhenPasswordIsWrong()
    {
        var user = CreateUser();
        _userRepository
            .Setup(x => x.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var strictHasher = new PasswordHasher<User>(
            Microsoft.Extensions.Options.Options.Create(new PasswordHasherOptions { IterationCount = 120_000 }));

        var service = new AuthService(
            _userRepository.Object,
            _roleRepository.Object,
            strictHasher,
            _tokenService.Object,
            _unitOfWork.Object,
            NullLogger<AuthService>.Instance);

        var originalHash = user.PasswordHash;

        await Assert.ThrowsAsync<UnauthorizedException>(
            () => service.LoginAsync(
                new LoginRequest { Email = "sara@example.com", Password = "WrongPass1" },
                CancellationToken.None));

        Assert.Equal(originalHash, user.PasswordHash);
        _unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetCurrentUserAsync_ReturnsUserDetails()
    {
        _userRepository
            .Setup(x => x.GetByIdAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateUser());

        var result = await _service.GetCurrentUserAsync(7, CancellationToken.None);

        Assert.Equal(7, result.Id);
        Assert.Equal("sara@example.com", result.Email);
        Assert.Equal(RoleNames.Customer, result.Role);
        Assert.True(result.IsActive);
    }

    [Fact]
    public async Task GetCurrentUserAsync_ThrowsNotFound_WhenUserIsMissing()
    {
        _userRepository
            .Setup(x => x.GetByIdAsync(99, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        await Assert.ThrowsAsync<NotFoundException>(
            () => _service.GetCurrentUserAsync(99, CancellationToken.None));
    }
}
