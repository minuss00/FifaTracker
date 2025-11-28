using FifaTracker.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FifaTracker.Application.Sessions.Queries.GetSessionDetails;

public class GetSessionDetailsQueryHandler : IRequestHandler<GetSessionDetailsQuery, SessionDetailsDto>
{
    private readonly IApplicationDbContext _context;

    public GetSessionDetailsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<SessionDetailsDto> Handle(GetSessionDetailsQuery request, CancellationToken cancellationToken)
    {
        var session = await _context.Sessions
            .Include(s => s.SessionUsers)
                .ThenInclude(su => su.User)
            .Include(s => s.Matches)
                .ThenInclude(m => m.MatchTeams)
                    .ThenInclude(mt => mt.User)
            .FirstOrDefaultAsync(s => s.Id == request.SessionId, cancellationToken);

        if (session == null)
            throw new KeyNotFoundException($"Session with ID {request.SessionId} not found");

        var users = session.SessionUsers.Select(su => new SessionUserDto(
            su.UserId,
            su.User.Name,
            su.JoinedAt
        )).ToList();

        var matches = session.Matches.Select(m => new MatchDto(
            m.Id,
            m.IsGenerated,
            m.IsCompleted,
            m.Team1Score,
            m.Team2Score,
            m.PlayedAt,
            m.MatchTeams.Where(mt => mt.TeamNumber == 1).Select(mt => new MatchPlayerDto(mt.UserId, mt.User.Name)).ToList(),
            m.MatchTeams.Where(mt => mt.TeamNumber == 2).Select(mt => new MatchPlayerDto(mt.UserId, mt.User.Name)).ToList()
        )).ToList();

        // Calculate player priorities for sorting
        var userIds = users.Select(u => u.UserId).ToList();
        var completedMatches = session.Matches.Where(m => m.IsCompleted).ToList();
        var playerStats = CalculatePlayerStats(userIds, completedMatches, users.ToDictionary(u => u.UserId, u => u.JoinedAt), session.StartDate, DateTime.UtcNow);

        // Sort matches so that matches with players who have played less are first
        // Be defensive: some matches may contain players who have been removed from the session
        // (we preserve completed matches). In that case, ignore players not present in playerStats
        // when computing the average priority for sorting.
        matches = matches.OrderByDescending(m =>
        {
            var priorities = m.Team1Players.Concat(m.Team2Players)
                .Select(mp => mp.UserId)
                .Where(id => playerStats.ContainsKey(id))
                .Select(id => playerStats[id].Priority)
                .ToList();

            return priorities.Any() ? priorities.Average() : 0.0;
        }).ToList();

        return new SessionDetailsDto(
            session.Id,
            session.Name,
            session.StartDate,
            session.EndDate,
            session.Status,
            session.MatchType,
            users,
            matches
        );
    }

    private Dictionary<Guid, PlayerMatchStats> CalculatePlayerStats(
        List<Guid> userIds,
        List<FifaTracker.Domain.Entities.Match> existingMatches,
        Dictionary<Guid, DateTime> userJoinTimes,
        DateTime sessionStartTime,
        DateTime now)
    {
        var stats = new Dictionary<Guid, PlayerMatchStats>();
        var sessionDuration = (now - sessionStartTime).TotalHours;

        foreach (var userId in userIds)
        {
            var joinTime = userJoinTimes.ContainsKey(userId) ? userJoinTimes[userId] : sessionStartTime;
            var timeInSession = (now - joinTime).TotalHours;
            var timeRatio = sessionDuration > 0 ? timeInSession / sessionDuration : 1.0;

            // Count matches for this player (including custom)
            var playerMatches = existingMatches
                .Where(m => m.MatchTeams.Any(mt => mt.UserId == userId))
                .ToList();

            var completedCount = playerMatches.Count(m => m.IsCompleted);
            var pendingCount = playerMatches.Count(m => !m.IsCompleted);

            // Calculate expected matches based on time in session
            var averageMatches = existingMatches.Count > 0
                ? existingMatches.SelectMany(m => m.MatchTeams).GroupBy(mt => mt.UserId).Average(g => g.Count())
                : 0;
            var expectedMatches = averageMatches * timeRatio;

            stats[userId] = new PlayerMatchStats
            {
                UserId = userId,
                TotalMatches = completedCount + pendingCount,
                CompletedMatches = completedCount,
                PendingMatches = pendingCount,
                TimeInSession = timeInSession,
                TimeRatio = timeRatio,
                ExpectedMatches = expectedMatches,
                Priority = expectedMatches - (completedCount + pendingCount),
                Teammates = new HashSet<Guid>(),
                Opponents = new HashSet<Guid>()
            };

            // Track who they've played with/against
            foreach (var match in playerMatches)
            {
                var playerTeamNumber = match.MatchTeams.First(mt => mt.UserId == userId).TeamNumber;
                foreach (var mt in match.MatchTeams.Where(mt => mt.UserId != userId))
                {
                    if (mt.TeamNumber == playerTeamNumber)
                    {
                        stats[userId].Teammates.Add(mt.UserId);
                    }
                    else
                    {
                        stats[userId].Opponents.Add(mt.UserId);
                    }
                }
            }
        }

        return stats;
    }

    private class PlayerMatchStats
    {
        public Guid UserId { get; set; }
        public int TotalMatches { get; set; }
        public int CompletedMatches { get; set; }
        public int PendingMatches { get; set; }
        public double TimeInSession { get; set; }
        public double TimeRatio { get; set; }
        public double ExpectedMatches { get; set; }
        public double Priority { get; set; }
        public HashSet<Guid> Teammates { get; set; } = new();
        public HashSet<Guid> Opponents { get; set; } = new();
    }
}
