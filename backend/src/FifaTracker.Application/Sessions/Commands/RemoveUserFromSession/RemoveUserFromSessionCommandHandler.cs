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

        // Remove all matches involving this user
        var userMatches = await _context.Matches
            .Where(m => m.SessionId == request.SessionId && m.MatchTeams.Any(mt => mt.UserId == request.UserId))
            .ToListAsync(cancellationToken);

        foreach (var match in userMatches)
        {
            _context.Matches.Remove(match);
        }

        // Get remaining users
        var remainingUserIds = session.SessionUsers
            .Where(su => su.UserId != request.UserId)
            .Select(su => su.UserId)
            .ToList();

        // Get remaining matches (should be none involving the removed user)
        var existingMatches = await _context.Matches
            .Where(m => m.SessionId == request.SessionId)
            .Include(m => m.MatchTeams)
            .ToListAsync(cancellationToken);

        // Build user join times dictionary for remaining users
        var userJoinTimes = session.SessionUsers
            .Where(su => su.UserId != request.UserId)
            .ToDictionary(su => su.UserId, su => su.JoinedAt);

        // Generate new matches for remaining users
        var newMatches = _matchGenerator.GenerateSmartMatches(
            session.Id,
            remainingUserIds,
            session.MatchType,
            5, // Generate 5 pending matches
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
