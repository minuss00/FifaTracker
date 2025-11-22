using FifaTracker.Application.Services;
using FifaTracker.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FifaTracker.Application.Sessions.Commands.ResumeUserInSession;

public class ResumeUserInSessionCommandHandler : IRequestHandler<ResumeUserInSessionCommand, Unit>
{
    private readonly IApplicationDbContext _context;
    private readonly IMatchGenerator _matchGenerator;

    public ResumeUserInSessionCommandHandler(IApplicationDbContext context, IMatchGenerator matchGenerator)
    {
        _context = context;
        _matchGenerator = matchGenerator;
    }

    public async Task<Unit> Handle(ResumeUserInSessionCommand request, CancellationToken cancellationToken)
    {
        var session = await _context.Sessions
            .Include(s => s.SessionUsers)
            .FirstOrDefaultAsync(s => s.Id == request.SessionId, cancellationToken);

        if (session == null)
            throw new KeyNotFoundException($"Session with ID {request.SessionId} not found");

        var sessionUser = session.SessionUsers.FirstOrDefault(su => su.UserId == request.UserId);
        if (sessionUser == null)
            throw new KeyNotFoundException($"User with ID {request.UserId} not found in session");

        if (sessionUser.IsActiveInSession)
            throw new InvalidOperationException("User is already active");

        var now = DateTime.UtcNow;
        sessionUser.IsActiveInSession = true;
        sessionUser.LastResumedAt = now;
        sessionUser.PausedAt = null;

        var existingMatches = await _context.GetMatchesForSessionAsync(request.SessionId, cancellationToken);
        _context.RemovePendingGeneratedMatches(existingMatches);

        var activeSessionUsers = session.SessionUsers.Where(su => su.IsActiveInSession).ToList();
        var activeUserIds = activeSessionUsers.Select(su => su.UserId).ToList();

        var minPlayers = session.MatchType switch
        {
            Domain.Entities.MatchType.OneVsOne => 2,
            Domain.Entities.MatchType.TwoVsOne => 3,
            _ => 4
        };

        if (activeUserIds.Count >= minPlayers)
        {
            var newMatches = _matchGenerator.GenerateSmartMatches(
                session.Id,
                activeUserIds,
                activeSessionUsers,
                session.MatchType,
                5,
                existingMatches,
                session.StartDate);

            foreach (var match in newMatches)
            {
                _context.Matches.Add(match);
            }
        }

        session.LastModifiedAt = now;
        await _context.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
