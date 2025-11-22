using FifaTracker.Application.Services;
using FifaTracker.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FifaTracker.Application.Matches.Commands.UpdateMatchScore;

public class UpdateMatchScoreCommandHandler : IRequestHandler<UpdateMatchScoreCommand, Unit>
{
    private readonly IApplicationDbContext _context;
    private readonly IMatchGenerator _matchGenerator;

    public UpdateMatchScoreCommandHandler(IApplicationDbContext context, IMatchGenerator matchGenerator)
    {
        _context = context;
        _matchGenerator = matchGenerator;
    }

    public async Task<Unit> Handle(UpdateMatchScoreCommand request, CancellationToken cancellationToken)
    {
        var match = await _context.Matches
            .Include(m => m.Session)
            .ThenInclude(s => s.SessionUsers)
            .FirstOrDefaultAsync(m => m.Id == request.MatchId, cancellationToken);

        if (match == null)
            throw new KeyNotFoundException($"Match with ID {request.MatchId} not found");

        match.Team1Score = request.Team1Score;
        match.Team2Score = request.Team2Score;
        match.IsCompleted = true;
        match.PlayedAt = DateTime.UtcNow;
        match.LastModifiedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        // Replace all pending generated matches with fresh ones based on current priorities
        if (match.Session.Status == Domain.Entities.SessionStatus.Active)
        {
            var allMatches = await _context.Matches
                .Where(m => m.SessionId == match.SessionId)
                .Include(m => m.MatchTeams)
                .ToListAsync(cancellationToken);

            // Remove all pending generated matches
            var pendingGeneratedMatches = allMatches
                .Where(m => !m.IsCompleted && m.IsGenerated)
                .ToList();
            
            foreach (var pendingMatch in pendingGeneratedMatches)
            {
                _context.Matches.Remove(pendingMatch);
                allMatches.Remove(pendingMatch); // Update list for generator
            }

            // Only generate for active users
            var activeSessionUsers = match.Session.SessionUsers.Where(su => su.IsActiveInSession).ToList();
            var activeUserIds = activeSessionUsers.Select(su => su.UserId).ToList();

            // Generate 5 fresh matches based on current priorities
            var newMatches = _matchGenerator.GenerateSmartMatches(
                match.SessionId,
                activeUserIds,
                activeSessionUsers,
                match.Session.MatchType,
                5, // Always generate 5
                allMatches,
                match.Session.StartDate);

            foreach (var newMatch in newMatches)
            {
                _context.Matches.Add(newMatch);
            }

            if (newMatches.Count > 0)
            {
                await _context.SaveChangesAsync(cancellationToken);
            }
        }

        return Unit.Value;
    }
}
