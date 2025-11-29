using FifaTracker.Application.Sessions.Queries.GetSessionDetails;
using FifaTracker.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FifaTracker.Application.Matches.GetCompletedMatches;

public class GetCompletedMatchesQueryHandler : IRequestHandler<GetCompletedMatchesQuery, List<MatchDto>>
{
    private readonly IApplicationDbContext _context;

    public GetCompletedMatchesQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<MatchDto>> Handle(GetCompletedMatchesQuery request, CancellationToken cancellationToken)
    {
        // Get completed matches sorted by PlayedAt/CreatedAt descending (most recent first)
        var completedMatches = await _context.Matches
            .Include(m => m.MatchTeams)
                .ThenInclude(mt => mt.User)
            .Where(m => m.SessionId == request.SessionId && m.IsCompleted)
            .OrderByDescending(m => m.PlayedAt ?? m.CreatedAt) // Most recently played first
            .ToListAsync(cancellationToken);

        // Map to DTO
        var matchDtos = completedMatches.Select(m => new MatchDto(
            m.Id,
            m.IsGenerated,
            m.IsCompleted,
            m.Team1Score,
            m.Team2Score,
            m.PlayedAt,
            m.MatchTeams.Where(mt => mt.TeamNumber == 1)
                .Select(mt => new MatchPlayerDto(mt.UserId, mt.User.Name))
                .ToList(),
            m.MatchTeams.Where(mt => mt.TeamNumber == 2)
                .Select(mt => new MatchPlayerDto(mt.UserId, mt.User.Name))
                .ToList()
        )).ToList();

        return matchDtos;
    }
}
