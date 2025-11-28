using FifaTracker.Application.Services;
using FifaTracker.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FifaTracker.Application.Sessions.Commands.RemoveUserFromSession;

public class RemoveUserFromSessionCommandHandler : IRequestHandler<RemoveUserFromSessionCommand, Unit>
{
    private readonly IApplicationDbContext _context;
    private readonly IMatchGenerator _matchGenerator;

    public RemoveUserFromSessionCommandHandler(IApplicationDbContext context, IMatchGenerator matchGenerator)
    {
        _context = context;
        _matchGenerator = matchGenerator;
    }

    public async Task<Unit> Handle(RemoveUserFromSessionCommand request, CancellationToken cancellationToken)
    {
        var session = await _context.Sessions
            .Include(s => s.SessionUsers)
            .FirstOrDefaultAsync(s => s.Id == request.SessionId, cancellationToken);

        if (session == null)
            throw new KeyNotFoundException($"Session with ID {request.SessionId} not found");

        // Check if user is in session
        var sessionUser = session.SessionUsers.FirstOrDefault(su => su.UserId == request.UserId);
        if (sessionUser == null)
            throw new InvalidOperationException("User is not in this session");

        // Remove user from session
        _context.SessionUsers.Remove(sessionUser);

        // Remove only pending generated matches that involve the removed user. Preserve completed matches to keep statistics intact.
        var pendingMatchesWithUser = await _context.Matches
            .Where(m => m.SessionId == request.SessionId && !m.IsCompleted && m.MatchTeams.Any(mt => mt.UserId == request.UserId))
            .ToListAsync(cancellationToken);

        if (pendingMatchesWithUser.Any())
        {
            _context.Matches.RemoveRange(pendingMatchesWithUser);
        }

        // After removing pending matches with the user, regenerate pending generated matches for remaining users
        // to keep a consistent number of pending matches. This mirrors the GenerateMoreMatches behavior which
        // clears existing generated pending matches and then generates new ones based on completed history.

        // Clear any remaining generated pending matches (they will be recreated by the generator)
        var remainingGeneratedPending = await _context.Matches
            .Where(m => m.SessionId == request.SessionId && m.IsGenerated && !m.IsCompleted)
            .ToListAsync(cancellationToken);

        _context.Matches.RemoveRange(remainingGeneratedPending);

        // Build the existingMatches list which will include completed matches and any custom matches (completed or not)
        var existingMatches = await _context.Matches
            .Where(m => m.SessionId == request.SessionId)
            .Include(m => m.MatchTeams)
            .ToListAsync(cancellationToken);

        // Get remaining users
        var remainingUserIds = session.SessionUsers
            .Where(su => su.UserId != request.UserId)
            .Select(su => su.UserId)
            .ToList();

        var userJoinTimes = session.SessionUsers
            .Where(su => su.UserId != request.UserId)
            .ToDictionary(su => su.UserId, su => su.JoinedAt);

        // Generate new pending matches for remaining users (default target 5)
        var newMatches = _matchGenerator.GenerateSmartMatches(
            session.Id,
            remainingUserIds,
            session.MatchType,
            5,
            existingMatches,
            userJoinTimes,
            session.StartDate);

        foreach (var match in newMatches)
        {
            _context.Matches.Add(match);
        }

        session.LastModifiedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
