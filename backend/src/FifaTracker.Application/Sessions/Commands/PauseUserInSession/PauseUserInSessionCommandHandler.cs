using FifaTracker.Application.Services;
using FifaTracker.Domain.Extensions;
using FifaTracker.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FifaTracker.Application.Sessions.Commands.PauseUserInSession;

public class PauseUserInSessionCommandHandler : IRequestHandler<PauseUserInSessionCommand, Unit>
{
    private readonly IApplicationDbContext _context;
    private readonly IMatchGenerator _matchGenerator;

    public PauseUserInSessionCommandHandler(IApplicationDbContext context, IMatchGenerator matchGenerator)
    {
        _context = context;
        _matchGenerator = matchGenerator;
    }

    public async Task<Unit> Handle(PauseUserInSessionCommand request, CancellationToken cancellationToken)
    {
        var session = await _context.Sessions
            .Include(s => s.SessionUsers)
            .FirstOrDefaultAsync(s => s.Id == request.SessionId, cancellationToken);

        if (session == null)
            throw new KeyNotFoundException($"Session with ID {request.SessionId} not found");

        var sessionUser = session.SessionUsers.FirstOrDefault(su => su.UserId == request.UserId);
        if (sessionUser == null)
            throw new KeyNotFoundException($"User with ID {request.UserId} not found in session");

        if (!sessionUser.IsActiveInSession)
            throw new InvalidOperationException("User is already paused");

        // Snapshot current active time before pausing
        sessionUser.SnapshotActiveTime(DateTime.UtcNow);
        sessionUser.IsActiveInSession = false;
        sessionUser.PausedAt = DateTime.UtcNow;

        // Remove all pending generated matches involving this user
        var pendingGeneratedMatches = await _context.Matches
            .Where(m => m.SessionId == request.SessionId && !m.IsCompleted && m.IsGenerated)
            .Include(m => m.MatchTeams)
            .ToListAsync(cancellationToken);

        var matchesToRemove = pendingGeneratedMatches
            .Where(m => m.MatchTeams.Any(mt => mt.UserId == request.UserId))
            .ToList();

        foreach (var match in matchesToRemove)
        {
            _context.Matches.Remove(match);
        }

        // Get remaining matches and active users
        var remainingMatches = pendingGeneratedMatches.Except(matchesToRemove).ToList();
        var completedAndCustomMatches = await _context.Matches
            .Where(m => m.SessionId == request.SessionId && (m.IsCompleted || !m.IsGenerated))
            .Include(m => m.MatchTeams)
            .ToListAsync(cancellationToken);

        var allExistingMatches = completedAndCustomMatches.Concat(remainingMatches).ToList();
        
        var activeUserIds = session.SessionUsers
            .Where(su => su.IsActiveInSession)
            .Select(su => su.UserId)
            .ToList();

        // Regenerate matches for active users only
        if (activeUserIds.Count >= (session.MatchType == Domain.Entities.MatchType.OneVsOne ? 2 : 
                                     session.MatchType == Domain.Entities.MatchType.TwoVsOne ? 3 : 4))
        {
            var targetCount = 5 - remainingMatches.Count;
            if (targetCount > 0)
            {
                var newMatches = _matchGenerator.GenerateSmartMatches(
                    session.Id,
                    activeUserIds,
                    session.SessionUsers.ToList(),
                    session.MatchType,
                    targetCount,
                    allExistingMatches,
                    session.StartDate);

                foreach (var match in newMatches)
                {
                    _context.Matches.Add(match);
                }
            }
        }

        session.LastModifiedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
