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

        // Map to DTO with TimesPlayed calculation
        var matchDtos = completedMatches.Select(m =>
        {
            var team1Ids = m.MatchTeams.Where(mt => mt.TeamNumber == 1).Select(mt => mt.UserId).OrderBy(id => id).ToList();
            var team2Ids = m.MatchTeams.Where(mt => mt.TeamNumber == 2).Select(mt => mt.UserId).OrderBy(id => id).ToList();
            
            // Count how many times this exact combination was played
            var timesPlayed = completedMatches.Count(cm =>
            {
                var cmTeam1Ids = cm.MatchTeams.Where(mt => mt.TeamNumber == 1).Select(mt => mt.UserId).OrderBy(id => id).ToList();
                var cmTeam2Ids = cm.MatchTeams.Where(mt => mt.TeamNumber == 2).Select(mt => mt.UserId).OrderBy(id => id).ToList();
                
                return (team1Ids.SequenceEqual(cmTeam1Ids) && team2Ids.SequenceEqual(cmTeam2Ids)) ||
                       (team1Ids.SequenceEqual(cmTeam2Ids) && team2Ids.SequenceEqual(cmTeam1Ids));
            });
            
            return new MatchDto(
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
                    .ToList(),
                timesPlayed
            );
        }).ToList();

        return matchDtos;
    }
}
