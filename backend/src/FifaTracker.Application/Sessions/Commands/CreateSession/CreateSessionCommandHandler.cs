using FifaTracker.Application.Services;
using FifaTracker.Domain.Entities;
using FifaTracker.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FifaTracker.Application.Sessions.Commands.CreateSession;

public class CreateSessionCommandHandler : IRequestHandler<CreateSessionCommand, Guid>
{
    private readonly IApplicationDbContext _context;
    private readonly IMatchGenerator _matchGenerator;

    public CreateSessionCommandHandler(IApplicationDbContext context, IMatchGenerator matchGenerator)
    {
        _context = context;
        _matchGenerator = matchGenerator;
    }

    public async Task<Guid> Handle(CreateSessionCommand request, CancellationToken cancellationToken)
    {
        // Validate session name
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new InvalidOperationException("Session name cannot be empty.");
        }

        if (request.Name.Length > 100)
        {
            throw new InvalidOperationException("Session name cannot be longer than 100 characters.");
        }

        // Validate user count
        if (request.UserIds == null || request.UserIds.Count < 2)
        {
            throw new InvalidOperationException("Session must have at least two players.");
        }

        // Validate that all users exist and are active
        var users = await _context.Users
            .Where(u => request.UserIds.Contains(u.Id))
            .ToListAsync(cancellationToken);

        if (users.Count != request.UserIds.Count)
        {
            throw new InvalidOperationException("One or more selected users do not exist.");
        }

        var inactiveUsers = users.Where(u => !u.IsActive).ToList();
        if (inactiveUsers.Any())
        {
            throw new InvalidOperationException($"Cannot create session with inactive users: {string.Join(", ", inactiveUsers.Select(u => u.Name))}");
        }

        // Validate match type requirements
        var minPlayers = request.MatchType switch
        {
            Domain.Entities.MatchType.OneVsOne => 2,
            Domain.Entities.MatchType.TwoVsTwo => 4,
            _ => 2
        };

        if (request.UserIds.Count < minPlayers)
        {
            throw new InvalidOperationException($"{request.MatchType} sessions require at least {minPlayers} players. You selected {request.UserIds.Count} player(s).");
        }

        var session = new Session
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            StartDate = DateTime.UtcNow,
            Status = SessionStatus.Active,
            MatchType = request.MatchType,
            CreatedAt = DateTime.UtcNow
        };

        _context.Sessions.Add(session);

        // Add users to session
        var sessionUsers = new List<SessionUser>();
        foreach (var userId in request.UserIds)
        {
            var sessionUser = new SessionUser
            {
                SessionId = session.Id,
                UserId = userId,
                JoinedAt = DateTime.UtcNow
            };
            _context.SessionUsers.Add(sessionUser);
            sessionUsers.Add(sessionUser);
        }
        
        await _context.SaveChangesAsync(cancellationToken);

        return session.Id;
    }
}
