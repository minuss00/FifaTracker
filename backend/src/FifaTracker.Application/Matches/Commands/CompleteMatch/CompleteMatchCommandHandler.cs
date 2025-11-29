using FifaTracker.Application.Services;
using FifaTracker.Domain.Entities;
using FifaTracker.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FifaTracker.Application.Matches.Commands.CompleteMatch;

public class CompleteMatchCommandHandler : IRequestHandler<CompleteMatchCommand, Guid>
{
    private readonly IApplicationDbContext _context;
    private readonly IMatchCombinationCache _cache;

    public CompleteMatchCommandHandler(IApplicationDbContext context, IMatchCombinationCache cache)
    {
        _context = context;
        _cache = cache;
    }

    public async Task<Guid> Handle(CompleteMatchCommand request, CancellationToken cancellationToken)
    {
        // Verify session exists
        var session = await _context.Sessions
            .FirstOrDefaultAsync(s => s.Id == request.SessionId, cancellationToken);

        if (session == null)
            throw new InvalidOperationException($"Session {request.SessionId} not found");

        // Create new match
        var match = new Match
        {
            Id = Guid.NewGuid(),
            SessionId = request.SessionId,
            IsGenerated = true,
            IsCompleted = true,
            Team1Score = request.Team1Score,
            Team2Score = request.Team2Score,
            PlayedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            LastModifiedAt = DateTime.UtcNow,
            MatchTeams = new List<MatchTeam>()
        };

        // Add team 1 players
        foreach (var userId in request.Team1UserIds)
        {
            match.MatchTeams.Add(new MatchTeam
            {
                Id = Guid.NewGuid(),
                MatchId = match.Id,
                UserId = userId,
                TeamNumber = 1
            });
        }

        // Add team 2 players
        foreach (var userId in request.Team2UserIds)
        {
            match.MatchTeams.Add(new MatchTeam
            {
                Id = Guid.NewGuid(),
                MatchId = match.Id,
                UserId = userId,
                TeamNumber = 2
            });
        }

        _context.Matches.Add(match);
        await _context.SaveChangesAsync(cancellationToken);

        // Increment TimesPlayed in cache for this combination
        _cache.IncrementTimesPlayed(match.SessionId, request.Team1UserIds, request.Team2UserIds);

        return match.Id;
    }
}
