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

        var existingMatches = await _context.Matches
            .Where(m => m.SessionId == request.SessionId)
            .Include(m => m.MatchTeams)
            .ToListAsync(cancellationToken);

        // Remove all pending generated matches before generating new ones
        var pendingGeneratedMatches = existingMatches
            .Where(m => !m.IsCompleted && m.IsGenerated)
            .ToList();
        
        foreach (var match in pendingGeneratedMatches)
        {
            _context.Matches.Remove(match);
            existingMatches.Remove(match); // Update list for generator
        }

        // Only generate for active users
        var activeSessionUsers = session.SessionUsers.Where(su => su.IsActiveInSession).ToList();
        var activeUserIds = activeSessionUsers.Select(su => su.UserId).ToList();

        var newMatches = _matchGenerator.GenerateSmartMatches(
            session.Id,
            activeUserIds,
            activeSessionUsers,
            session.MatchType,
            request.TargetCount,
            existingMatches,
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
