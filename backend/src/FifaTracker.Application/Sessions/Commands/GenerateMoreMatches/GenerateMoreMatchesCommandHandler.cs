using FifaTracker.Application.Services;
using FifaTracker.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FifaTracker.Application.Sessions.Commands.GenerateMoreMatches;

public class GenerateMoreMatchesCommandHandler : IRequestHandler<GenerateMoreMatchesCommand, int>
{
    private readonly IApplicationDbContext _context;
    private readonly IMatchGenerator _matchGenerator;

    public GenerateMoreMatchesCommandHandler(IApplicationDbContext context, IMatchGenerator matchGenerator)
    {
        _context = context;
        _matchGenerator = matchGenerator;
    }

    public async Task<int> Handle(GenerateMoreMatchesCommand request, CancellationToken cancellationToken)
    {
        var session = await _context.Sessions
            .Include(s => s.SessionUsers)
            .FirstOrDefaultAsync(s => s.Id == request.SessionId, cancellationToken);

        if (session == null)
            throw new KeyNotFoundException($"Session with ID {request.SessionId} not found");

        if (session.Status != Domain.Entities.SessionStatus.Active)
            throw new InvalidOperationException("Cannot generate matches for inactive session");

        // Clear existing generated matches before generating new ones
        var existingGeneratedMatches = await _context.Matches
            .Where(m => m.SessionId == request.SessionId && m.IsGenerated && !m.IsCompleted)
            .ToListAsync(cancellationToken);

        _context.Matches.RemoveRange(existingGeneratedMatches);

        var existingMatches = await _context.Matches
            .Where(m => m.SessionId == request.SessionId)
            .Include(m => m.MatchTeams)
            .ToListAsync(cancellationToken);

        var userIds = session.SessionUsers.Select(su => su.UserId).ToList();
        var userJoinTimes = session.SessionUsers.ToDictionary(su => su.UserId, su => su.JoinedAt);

        var newMatches = _matchGenerator.GenerateSmartMatches(
            session.Id,
            userIds,
            session.MatchType,
            request.TargetCount,
            existingMatches,
            userJoinTimes,
            session.StartDate);

        foreach (var match in newMatches)
        {
            _context.Matches.Add(match);
        }

        session.LastModifiedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        return newMatches.Count;
    }
}
