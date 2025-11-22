using FifaTracker.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FifaTracker.Application.Users.Queries.GetLeaderboard;

public class GetLeaderboardQueryHandler : IRequestHandler<GetLeaderboardQuery, List<LeaderboardEntryDto>>
{
    private readonly IApplicationDbContext _context;

    public GetLeaderboardQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<LeaderboardEntryDto>> Handle(GetLeaderboardQuery request, CancellationToken cancellationToken)
    {
        var users = await _context.Users
            .AsNoTracking()
            .Where(u => u.IsActive)
            .ToListAsync(cancellationToken);
        var leaderboard = new List<LeaderboardEntryDto>();

        foreach (var user in users)
        {
            // Get all matches where user participated
            var userMatches = await _context.MatchTeams
                .AsNoTracking()
                .Include(mt => mt.Match)
                .Where(mt => mt.UserId == user.Id && mt.Match.IsCompleted)
                .ToListAsync(cancellationToken);

            if (userMatches.Count == 0)
            {
                // User hasn't played any matches
                leaderboard.Add(new LeaderboardEntryDto
                {
                    UserId = user.Id,
                    UserName = user.Name,
                    TotalMatches = 0,
                    Wins = 0,
                    Losses = 0,
                    Draws = 0,
                    GoalsScored = 0,
                    GoalsConceded = 0,
                    GoalDifference = 0,
                    WinRate = 0,
                    Points = 0
                });
                continue;
            }

            var stats = new LeaderboardEntryDto
            {
                UserId = user.Id,
                UserName = user.Name
            };

            var processedMatches = new HashSet<Guid>();

            foreach (var userMatch in userMatches)
            {
                if (processedMatches.Contains(userMatch.MatchId))
                    continue;

                processedMatches.Add(userMatch.MatchId);

                var match = userMatch.Match;

                // Determine which team the user is on
                var userTeamNumber = userMatch.TeamNumber;
                var opponentTeamNumber = userTeamNumber == 1 ? 2 : 1;

                var userTeamScore = userTeamNumber == 1 ? match.Team1Score!.Value : match.Team2Score!.Value;
                var opponentTeamScore = userTeamNumber == 1 ? match.Team2Score!.Value : match.Team1Score!.Value;

                stats.TotalMatches++;
                stats.GoalsScored += userTeamScore;
                stats.GoalsConceded += opponentTeamScore;

                if (userTeamScore > opponentTeamScore)
                {
                    stats.Wins++;
                    stats.Points += 3;
                }
                else if (userTeamScore < opponentTeamScore)
                {
                    stats.Losses++;
                }
                else
                {
                    stats.Draws++;
                    stats.Points += 1;
                }
            }

            stats.GoalDifference = stats.GoalsScored - stats.GoalsConceded;
            stats.WinRate = stats.TotalMatches > 0
                ? Math.Round((decimal)stats.Wins / stats.TotalMatches * 100, 2)
                : 0;

            leaderboard.Add(stats);
        }

        // Sort by points (descending), then by goal difference, then by goals scored
        return leaderboard
            .OrderByDescending(l => l.Points)
            .ThenByDescending(l => l.GoalDifference)
            .ThenByDescending(l => l.GoalsScored)
            .ToList();
    }
}
