using FifaTracker.Application.Services;
using FifaTracker.Domain.Entities;
using FifaTracker.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FifaTracker.Application.Matches.GetPendingMatches;

public class GetPendingMatchesQueryHandler : IRequestHandler<GetPendingMatchesQuery, List<Match>>
{
    private readonly IApplicationDbContext _context;
    private readonly IMatchGenerator _matchGenerator;

    public GetPendingMatchesQueryHandler(IApplicationDbContext context, IMatchGenerator matchGenerator)
    {
        _context = context;
        _matchGenerator = matchGenerator;
    }

    public async Task<List<Match>> Handle(GetPendingMatchesQuery request, CancellationToken cancellationToken)
    {
        // Get session with users
        var session = await _context.Sessions
            .Include(s => s.SessionUsers)
            .FirstOrDefaultAsync(s => s.Id == request.SessionId, cancellationToken);

        if (session == null)
            throw new InvalidOperationException($"Session {request.SessionId} not found");

        // Get active user IDs (not paused)
        var activeUserIds = session.SessionUsers
            .Where(su => su.PausedAt == null)
            .Select(su => su.UserId)
            .ToList();

        if (activeUserIds.Count < 2)
            return new List<Match>(); // Not enough players

        // Get completed matches from database
        var completedMatches = await _context.Matches
            .Include(m => m.MatchTeams)
            .Where(m => m.SessionId == request.SessionId && m.IsCompleted)
            .ToListAsync(cancellationToken);

        // Get custom pending matches from database (user-created, not auto-generated)
        var customPendingMatches = await _context.Matches
            .Include(m => m.MatchTeams)
            .Where(m => m.SessionId == request.SessionId && !m.IsCompleted && !m.IsGenerated)
            .ToListAsync(cancellationToken);

        // Set priority for custom matches (always first)
        foreach (var customMatch in customPendingMatches)
        {
            customMatch.Priority = double.MaxValue;
        }

        // Generate pending matches in memory
        var generatedPendingMatches = _matchGenerator.GetPendingMatches(
            request.SessionId,
            activeUserIds,
            session.SessionUsers.ToList(),
            session.MatchType,
            completedMatches,
            session.CreatedAt);

        // Combine custom and generated pending matches
        var allPendingMatches = customPendingMatches
            .Concat(generatedPendingMatches)
            .OrderByDescending(m => m.Priority) // Custom first (Double.MaxValue), then by priority
            .ToList();

        return allPendingMatches;
    }
}
