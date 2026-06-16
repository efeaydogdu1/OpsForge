using MediatR;
using Microsoft.EntityFrameworkCore;
using OpsForge.Domain;

namespace OpsForge.Application;

public sealed class IdentityHandlers(IAppDbContext db, IPasswordHasher passwordHasher, ITokenService tokenService)
    : IRequestHandler<RegisterCommand, AuthResponse>, IRequestHandler<LoginCommand, AuthResponse>, IRequestHandler<RefreshTokenCommand, AuthResponse>
{
    public async Task<AuthResponse> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var existingUser = await db.Users.FirstOrDefaultAsync(x => x.Email == email, cancellationToken);
        if (existingUser is not null)
        {
            throw new InvalidOperationException("Email is already registered.");
        }

        var user = new AppUser
        {
            Email = email,
            DisplayName = request.DisplayName.Trim(),
            PasswordHash = passwordHasher.Hash(request.Password),
            Role = UserRole.Engineer
        };

        db.Add(user);
        var refreshToken = tokenService.CreateRefreshToken();
        db.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = tokenService.HashRefreshToken(refreshToken),
            ExpiresAtUtc = DateTime.UtcNow.AddDays(14)
        });

        await db.SaveChangesAsync(cancellationToken);
        return new AuthResponse(user.Id, user.Email, user.DisplayName, user.Role.ToString(), tokenService.CreateAccessToken(user), refreshToken);
    }

    public async Task<AuthResponse> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await db.Users.FirstOrDefaultAsync(x => x.Email == email, cancellationToken)
            ?? throw new InvalidOperationException("Invalid credentials.");

        if (!passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            throw new InvalidOperationException("Invalid credentials.");
        }

        var refreshToken = tokenService.CreateRefreshToken();
        db.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = tokenService.HashRefreshToken(refreshToken),
            ExpiresAtUtc = DateTime.UtcNow.AddDays(14)
        });

        await db.SaveChangesAsync(cancellationToken);
        return new AuthResponse(user.Id, user.Email, user.DisplayName, user.Role.ToString(), tokenService.CreateAccessToken(user), refreshToken);
    }

    public async Task<AuthResponse> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        var tokenHash = tokenService.HashRefreshToken(request.RefreshToken);
        var refreshToken = await db.RefreshTokens.FirstOrDefaultAsync(x => x.TokenHash == tokenHash && x.RevokedAtUtc == null && x.ExpiresAtUtc > DateTime.UtcNow, cancellationToken)
            ?? throw new InvalidOperationException("Invalid refresh token.");

        var user = await db.Users.FirstAsync(x => x.Id == refreshToken.UserId, cancellationToken);
        var newRefreshToken = tokenService.CreateRefreshToken();
        refreshToken.RevokedAtUtc = DateTime.UtcNow;
        refreshToken.ReplacedByTokenHash = tokenService.HashRefreshToken(newRefreshToken);
        db.Update(refreshToken);
        db.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = refreshToken.ReplacedByTokenHash,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(14)
        });

        await db.SaveChangesAsync(cancellationToken);
        return new AuthResponse(user.Id, user.Email, user.DisplayName, user.Role.ToString(), tokenService.CreateAccessToken(user), newRefreshToken);
    }
}
