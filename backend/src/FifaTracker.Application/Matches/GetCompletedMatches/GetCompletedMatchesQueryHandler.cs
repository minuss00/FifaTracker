using FifaTracker.Domain.Entities;
using FifaTracker.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FifaTracker.Application.Matches.GetCompletedMatches;

public class GetCompletedMatchesQueryHandler : IRequestHandler<GetCompletedMatchesQuery, List<Match>>
{
    private readonly IApplicationDbContext _context;

    public GetCompletedMatchesQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<Match>> Handle(GetCompletedMatchesQuery request, CancellationToken cancellationToken)
    {
        // Get completed matches sorted by PlayedAt/CreatedAt descending (most recent first)
        var completedMatches = await _context.Matches
            .Include(m => m.MatchTeams)
                .ThenInclude(mt => mt.User)
            .Where(m => m.SessionId == request.SessionId && m.IsCompleted)
            .OrderByDescending(m => m.PlayedAt ?? m.CreatedAt) // Most recently played first
            .ToListAsync(cancellationToken);

        return completedMatches;
    }
}
