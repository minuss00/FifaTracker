using FifaTracker.Application.Services;
using FifaTracker.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FifaTracker.Application.Matches.Commands.UpdateMatchScore;

public class UpdateMatchScoreCommandHandler : IRequestHandler<UpdateMatchScoreCommand, Unit>
{
    private readonly IApplicationDbContext _context;
    private readonly IMatchCombinationCache _cache;

    public UpdateMatchScoreCommandHandler(IApplicationDbContext context, IMatchCombinationCache cache)
    {
        _context = context;
        _cache = cache;
    }

    public async Task<Unit> Handle(UpdateMatchScoreCommand request, CancellationToken cancellationToken)
    {
        var match = await _context.Matches
            .Include(m => m.MatchTeams)
            .FirstOrDefaultAsync(m => m.Id == request.MatchId, cancellationToken);

        if (match == null)
            throw new KeyNotFoundException($"Match with ID {request.MatchId} not found");

        match.Team1Score = request.Team1Score;
        match.Team2Score = request.Team2Score;
        match.IsCompleted = true;
        match.PlayedAt = DateTime.UtcNow;
        match.LastModifiedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        // Increment TimesPlayed in cache for this combination
        var team1Ids = match.MatchTeams.Where(mt => mt.TeamNumber == 1).Select(mt => mt.UserId).ToList();
        var team2Ids = match.MatchTeams.Where(mt => mt.TeamNumber == 2).Select(mt => mt.UserId).ToList();
        _cache.IncrementTimesPlayed(match.SessionId, team1Ids, team2Ids);

        return Unit.Value;
    }
}
