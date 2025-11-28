using FifaTracker.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FifaTracker.Application.Matches.Commands.UpdateMatchScore;

public class UpdateMatchScoreCommandHandler : IRequestHandler<UpdateMatchScoreCommand, Unit>
{
    private readonly IApplicationDbContext _context;

    public UpdateMatchScoreCommandHandler(IApplicationDbContext context)
    {
        _context = context;
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

        return Unit.Value;
    }
}
