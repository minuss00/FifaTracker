using FifaTracker.Application.Services;
using FifaTracker.Domain.Entities;
using FifaTracker.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FifaTracker.Application.Sessions.Commands.EndSession;

public class EndSessionCommandHandler : IRequestHandler<EndSessionCommand, Unit>
{
    private readonly IApplicationDbContext _context;
    private readonly IMatchCombinationCache _cache;

    public EndSessionCommandHandler(IApplicationDbContext context, IMatchCombinationCache cache)
    {
        _context = context;
        _cache = cache; 
    }

    public async Task<Unit> Handle(EndSessionCommand request, CancellationToken cancellationToken)
    {
        var session = await _context.Sessions.FirstOrDefaultAsync(s => s.Id == request.SessionId, cancellationToken);

        if (session == null)
            throw new KeyNotFoundException($"Session with ID {request.SessionId} not found");

        session.Status = SessionStatus.Completed;
        session.EndDate = DateTime.UtcNow;
        session.LastModifiedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        // Clear all matches that are not completed
        var incompleteMatches = await _context.Matches
            .Where(m => m.SessionId == request.SessionId && !m.IsCompleted)
            .ToListAsync(cancellationToken);

        _context.Matches.RemoveRange(incompleteMatches);

        _cache.ClearMatchCombinations(session.Id);

        return Unit.Value;
    }
}
