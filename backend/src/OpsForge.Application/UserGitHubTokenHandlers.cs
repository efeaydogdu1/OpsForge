using MediatR;
using Microsoft.EntityFrameworkCore;
using OpsForge.Domain;

namespace OpsForge.Application;

public sealed class UserGitHubTokenHandlers(
    IAppDbContext db,
    ICurrentUserContext currentUser,
    ISecretProtector secretProtector,
    IAuditService audit)
    : IRequestHandler<CreateUserGitHubTokenCommand, UserGitHubTokenDto>,
      IRequestHandler<UpdateUserGitHubTokenCommand, UserGitHubTokenDto>,
      IRequestHandler<DeleteUserGitHubTokenCommand, Unit>
{
    public async Task<UserGitHubTokenDto> Handle(CreateUserGitHubTokenCommand request, CancellationToken cancellationToken)
    {
        var userId = RequireUserId();
        var name = NormalizeName(request.Name);
        var token = request.Token.Trim();

        if (string.IsNullOrWhiteSpace(token))
        {
            throw new ArgumentException("GitHub token is required.", nameof(request));
        }

        var hasTokens = await db.UserGitHubTokens.AnyAsync(x => x.UserId == userId, cancellationToken);
        if (request.IsDefault || !hasTokens)
        {
            await ClearDefaultAsync(userId, cancellationToken);
        }

        var entity = new UserGitHubToken
        {
            UserId = userId,
            Name = name,
            EncryptedToken = secretProtector.Protect(token),
            TokenLastFour = token.Length <= 4 ? token : token[^4..],
            IsDefault = request.IsDefault || !hasTokens,
            IsActive = true
        };

        db.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        await audit.LogAsync(AuditAction.Create, nameof(UserGitHubToken), entity.Id.ToString(), userId, $"Created GitHub token '{entity.Name}'", cancellationToken);

        return Map(entity);
    }

    public async Task<UserGitHubTokenDto> Handle(UpdateUserGitHubTokenCommand request, CancellationToken cancellationToken)
    {
        var userId = RequireUserId();
        var entity = await db.UserGitHubTokens.FirstOrDefaultAsync(x => x.Id == request.Id && x.UserId == userId, cancellationToken)
            ?? throw new KeyNotFoundException("GitHub token not found.");

        if (request.IsDefault)
        {
            await ClearDefaultAsync(userId, cancellationToken);
        }

        entity.Name = NormalizeName(request.Name);
        entity.IsDefault = request.IsDefault;
        entity.IsActive = request.IsActive;
        db.Update(entity);

        await db.SaveChangesAsync(cancellationToken);
        await audit.LogAsync(AuditAction.Update, nameof(UserGitHubToken), entity.Id.ToString(), userId, $"Updated GitHub token '{entity.Name}'", cancellationToken);

        return Map(entity);
    }

    public async Task<Unit> Handle(DeleteUserGitHubTokenCommand request, CancellationToken cancellationToken)
    {
        var userId = RequireUserId();
        var entity = await db.UserGitHubTokens.FirstOrDefaultAsync(x => x.Id == request.Id && x.UserId == userId, cancellationToken)
            ?? throw new KeyNotFoundException("GitHub token not found.");

        entity.IsDeleted = true;
        entity.IsActive = false;
        entity.IsDefault = false;
        db.Update(entity);

        await db.SaveChangesAsync(cancellationToken);
        await audit.LogAsync(AuditAction.Delete, nameof(UserGitHubToken), entity.Id.ToString(), userId, $"Deleted GitHub token '{entity.Name}'", cancellationToken);

        return Unit.Value;
    }

    private Guid RequireUserId() =>
        currentUser.UserId ?? throw new UnauthorizedAccessException("A signed-in user is required.");

    private async Task ClearDefaultAsync(Guid userId, CancellationToken cancellationToken)
    {
        var existingDefaults = await db.UserGitHubTokens
            .Where(x => x.UserId == userId && x.IsDefault)
            .ToListAsync(cancellationToken);

        foreach (var token in existingDefaults)
        {
            token.IsDefault = false;
            db.Update(token);
        }
    }

    private static string NormalizeName(string name)
    {
        var normalized = name.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new ArgumentException("Token name is required.", nameof(name));
        }

        return normalized;
    }

    public static UserGitHubTokenDto Map(UserGitHubToken token) =>
        new(token.Id, token.Name, token.TokenLastFour, token.IsDefault, token.IsActive, token.CreatedAtUtc, token.LastUsedAtUtc);
}
