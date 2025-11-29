using FifaTracker.Application.Sessions.Queries.GetSessionDetails;
using FifaTracker.Domain.Entities;
using FifaTracker.Domain.Extensions;
using FifaTracker.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace FifaTracker.Application.Services;

public class MatchGenerator : IMatchGenerator
{
    private readonly IApplicationDbContext _context;
    private readonly IMatchCombinationCache _cache;

    public MatchGenerator(IApplicationDbContext context, IMatchCombinationCache cache)
    {
        _context = context;
        _cache = cache;
    }

    public async Task<List<MatchDto>> GetPendingMatchesAsync(Session session, CancellationToken cancellationToken)
    {
        // Get active user IDs (not paused)
        var activeUserIds = session.SessionUsers
            .Where(su => su.PausedAt == null)
            .Select(su => su.UserId)
            .ToList();

        if (activeUserIds.Count < 2)
        {
            return new List<MatchDto>();
        }

        // Get completed matches from database
        var completedMatches = await _context.Matches
            .Include(m => m.MatchTeams)
            .Where(m => m.SessionId == session.Id && m.IsCompleted)
            .ToListAsync(cancellationToken);

        // Get custom pending matches from database (user-created, not auto-generated)
        var customPendingMatches = await _context.Matches
            .Include(m => m.MatchTeams)
            .Where(m => m.SessionId == session.Id && !m.IsCompleted && !m.IsGenerated)
            .ToListAsync(cancellationToken);


        // Check if cache has combinations for this session, if not - generate and store
        if (!_cache.HasCombinationsForSession(session.Id))
        {
            var newCombinations = GenerateAllCombinations(session.Id, activeUserIds, session.MatchType);
            
            // Initialize TimesPlayed from existing completed matches
            InitializeTimesPlayed(newCombinations, completedMatches, customPendingMatches);
            
            _cache.StoreCombinationsForSession(session.Id, newCombinations);
        }

        // Get combinations from cache (TimesPlayed is alredy up-to-date from IncrementTimesPlayed calls)
        var cachedCombinations = _cache.GetCombinationsForSession(session.Id);

        // Get last completed match for priority calculations
        var lastMatch = completedMatches
            .OrderByDescending(m => m.PlayedAt)
            .FirstOrDefault();

        // Calculate priorities and sort
        var sortedCombinations = CalculatePrioritiesAndSort(
            cachedCombinations,
            session.SessionUsers.ToList(),
            lastMatch,
            session.StartDate);

        // Convert to DTOs
        var userNameLookup = session.SessionUsers.ToDictionary(su => su.UserId, su => su.User.Name);

        var generatedMatchDtos = sortedCombinations
            .Select(combo => ConvertToMatchDto(combo, userNameLookup))
            .ToList();

        var customMatchDtos = customPendingMatches
            .Select(m => new MatchDto(
                m.Id,
                false, // IsGenerated = false for custom matches
                false, // IsCompleted = false
                m.Team1Score,
                m.Team2Score,
                m.PlayedAt,
                m.MatchTeams
                    .Where(mt => mt.TeamNumber == 1)
                    .Select(mt => new MatchPlayerDto(mt.UserId, userNameLookup.GetValueOrDefault(mt.UserId, "Unknown")))
                    .ToList(),
                m.MatchTeams
                    .Where(mt => mt.TeamNumber == 2)
                    .Select(mt => new MatchPlayerDto(mt.UserId, userNameLookup.GetValueOrDefault(mt.UserId, "Unknown")))
                    .ToList()
            ))
            .ToList();

        // Return custom matches first, then generated matches
        return customMatchDtos.Concat(generatedMatchDtos).ToList();
    }

    private List<CachedMatchCombination> GenerateAllCombinations(
        Guid sessionId,
        List<Guid> userIds,
        Domain.Entities.MatchType matchType)
    {
        return matchType switch
        {
            Domain.Entities.MatchType.OneVsOne => GenerateOneVsOne(sessionId, userIds),
            Domain.Entities.MatchType.TwoVsTwo => GenerateTwoVsTwo(sessionId, userIds),
            Domain.Entities.MatchType.TwoVsOne => GenerateTwoVsOne(sessionId, userIds),
            _ => new List<CachedMatchCombination>()
        };
    }

    private List<CachedMatchCombination> GenerateOneVsOne(Guid sessionId, List<Guid> userIds)
    {
        var combinations = new List<CachedMatchCombination>();
        if (userIds.Count < 2) return combinations;

        for (int i = 0; i < userIds.Count - 1; i++)
        {
            for (int j = i + 1; j < userIds.Count; j++)
            {
                combinations.Add(new CachedMatchCombination
                {
                    SessionId = sessionId,
                    Team1UserIds = new List<Guid> { userIds[i] },
                    Team2UserIds = new List<Guid> { userIds[j] },
                    TimesPlayed = 0
                });
            }
        }

        return combinations;
    }

    private List<CachedMatchCombination> GenerateTwoVsTwo(Guid sessionId, List<Guid> userIds)
    {
        var combinations = new List<CachedMatchCombination>();
        if (userIds.Count < 4) return combinations;

        var players = userIds.ToArray();
        var n = players.Length;

        var teams = new List<List<Guid>>();
        for (int i = 0; i < n - 1; i++)
        {
            for (int j = i + 1; j < n; j++)
            {
                teams.Add(new List<Guid> { players[i], players[j] });
            }
        }

        var uniqueKeys = new HashSet<string>();

        for (int i = 0; i < teams.Count - 1; i++)
        {
            for (int j = i + 1; j < teams.Count; j++)
            {
                var team1 = teams[i];
                var team2 = teams[j];

                if (!team1.Any(p => team2.Contains(p)))
                {
                    var combo = new CachedMatchCombination
                    {
                        SessionId = sessionId,
                        Team1UserIds = team1,
                        Team2UserIds = team2,
                        TimesPlayed = 0
                    };

                    var key = GetCombinationKey(combo.Team1UserIds, combo.Team2UserIds);
                    if (uniqueKeys.Add(key))
                    {
                        combinations.Add(combo);
                    }
                }
            }
        }

        return combinations;
    }

    private List<CachedMatchCombination> GenerateTwoVsOne(Guid sessionId, List<Guid> userIds)
    {
        var combinations = new List<CachedMatchCombination>();
        if (userIds.Count < 3) return combinations;

        for (int solo = 0; solo < userIds.Count; solo++)
        {
            for (int i = 0; i < userIds.Count - 1; i++)
            {
                if (i == solo) continue;

                for (int j = i + 1; j < userIds.Count; j++)
                {
                    if (j == solo) continue;

                    combinations.Add(new CachedMatchCombination
                    {
                        SessionId = sessionId,
                        Team1UserIds = new List<Guid> { userIds[i], userIds[j] },
                        Team2UserIds = new List<Guid> { userIds[solo] },
                        TimesPlayed = 0
                    });
                }
            }
        }

        return combinations;
    }

    private void InitializeTimesPlayed(
        List<CachedMatchCombination> combinations,
        List<Match> completedMatches,
        List<Match> customPendingMatches)
    {
        // Count from completed matches (both generated and custom)
        var allCompletedMatches = completedMatches.Concat(customPendingMatches.Where(m => m.IsCompleted));

        foreach (var match in allCompletedMatches)
        {
            var team1Ids = match.MatchTeams.Where(mt => mt.TeamNumber == 1).Select(mt => mt.UserId).ToList();
            var team2Ids = match.MatchTeams.Where(mt => mt.TeamNumber == 2).Select(mt => mt.UserId).ToList();

            var matchingCombo = combinations.FirstOrDefault(c =>
                MatchesTeamComposition(c.Team1UserIds, c.Team2UserIds, team1Ids, team2Ids));

            if (matchingCombo != null)
            {
                matchingCombo.TimesPlayed++;
            }
        }
    }

    private bool MatchesTeamComposition(
        List<Guid> combo1Team1, List<Guid> combo1Team2,
        List<Guid> combo2Team1, List<Guid> combo2Team2)
    {
        var c1t1 = combo1Team1.OrderBy(id => id).ToList();
        var c1t2 = combo1Team2.OrderBy(id => id).ToList();
        var c2t1 = combo2Team1.OrderBy(id => id).ToList();
        var c2t2 = combo2Team2.OrderBy(id => id).ToList();

        return (c1t1.SequenceEqual(c2t1) && c1t2.SequenceEqual(c2t2)) ||
               (c1t1.SequenceEqual(c2t2) && c1t2.SequenceEqual(c2t1));
    }

    private List<CachedMatchCombination> CalculatePrioritiesAndSort(
        List<CachedMatchCombination> combinations,
        List<SessionUser> sessionUsers,
        Match? lastMatch,
        DateTime sessionStartTime)
    {
        var now = DateTime.UtcNow;

        var minTimesPlayed = combinations.Min(c => c.TimesPlayed);

        foreach (var combo in combinations)
        {
            combo.Priority = CalculatePriority(combo, combinations, sessionUsers, lastMatch, minTimesPlayed, now);
        }

        return combinations.OrderByDescending(c => c.Priority).ToList();
    }

    private double CalculatePriority(
        CachedMatchCombination combo,
        List<CachedMatchCombination> combinations,
        List<SessionUser> sessionUsers,
        Match? lastMatch,
        int minTimesPlayed,
        DateTime now)
    {
        // FIRST: Check if any team from last match repeats - highest priority check
        if (lastMatch != null)
        {
            var lastTeam1 = lastMatch.MatchTeams
                .Where(mt => mt.TeamNumber == 1)
                .Select(mt => mt.UserId)
                .OrderBy(id => id)
                .ToList();
            var lastTeam2 = lastMatch.MatchTeams
                .Where(mt => mt.TeamNumber == 2)
                .Select(mt => mt.UserId)
                .OrderBy(id => id)
                .ToList();
            var currentTeam1 = combo.Team1UserIds.OrderBy(id => id).ToList();
            var currentTeam2 = combo.Team2UserIds.OrderBy(id => id).ToList();

            var teamRepeatsSameOrientation = lastTeam1.SequenceEqual(currentTeam1) || lastTeam2.SequenceEqual(currentTeam2);
            var teamRepeatsReversed = lastTeam1.SequenceEqual(currentTeam2) || lastTeam2.SequenceEqual(currentTeam1);

            if (teamRepeatsSameOrientation || teamRepeatsReversed)
            {
                return double.MinValue; // Extremely high penalty
            }
        }

        var fairnessScore = 0.0;

        // If this match was played more than minimum AND not all have been played, penalize
        if (combo.TimesPlayed > minTimesPlayed)
        {
            fairnessScore += -1000.0 - (combo.TimesPlayed * 100); // Penalty increases with times played
        }

        // Calculate fairness score based on time-proportional fairness
        var maxActiveTime = sessionUsers.Max(su => su.GetCurrentActiveTotalHours(now));
        var maxMatchesPlayed = sessionUsers.Max(su =>
        {
            var count = combinations
                .Where(c => c.TimesPlayed > 0)
                .Where(c => c.Team1UserIds.Contains(su.UserId) || c.Team2UserIds.Contains(su.UserId))
                .Sum(c => c.TimesPlayed);
            return count;
        });

        var playerIds = combo.Team1UserIds.Concat(combo.Team2UserIds).ToList();
        foreach (var playerId in playerIds)
        {
            var sessionUser = sessionUsers.FirstOrDefault(su => su.UserId == playerId);
            if (sessionUser == null) continue;

            var activeTime = sessionUser.GetCurrentActiveTotalHours(now);

            var playerMatchCount = combinations
                .Where(c => c.TimesPlayed > 0)
                .Where(c => c.Team1UserIds.Contains(playerId) || c.Team2UserIds.Contains(playerId))
                .Sum(c => c.TimesPlayed);

            var timeRatio = maxActiveTime > 0 ? activeTime / maxActiveTime : 1.0;
            var expectedMatches = maxMatchesPlayed * timeRatio;

            var matchesDeficit = expectedMatches - playerMatchCount;
            fairnessScore += matchesDeficit * 100.0;
        }

        // Penalty for repetition: if any player was in the last match
        var repetitionPenalty = 0.0;
        if (lastMatch != null)
        {
            var lastMatchPlayerIds = lastMatch.MatchTeams.Select(mt => mt.UserId).ToList();
            var repeatCount = playerIds.Count(pid => lastMatchPlayerIds.Contains(pid));
            repetitionPenalty = repeatCount * 50.0;
        }

        // Bonus for matches played fewer times
        var timesPlayedBonus = (minTimesPlayed - combo.TimesPlayed) * 100.0;

        return (fairnessScore * 100.0) + timesPlayedBonus - repetitionPenalty;
    }

    private MatchDto ConvertToMatchDto(CachedMatchCombination combo, Dictionary<Guid, string> userNameLookup)
    {
        return new MatchDto(
            Guid.NewGuid(),
            true, // IsGenerated = true
            false, // IsCompleted = false
            null,
            null,
            null,
            combo.Team1UserIds.Select(id => new MatchPlayerDto(
                id,
                userNameLookup.GetValueOrDefault(id, "Unknown")
            )).ToList(),
            combo.Team2UserIds.Select(id => new MatchPlayerDto(
                id,
                userNameLookup.GetValueOrDefault(id, "Unknown")
            )).ToList()
        );
    }

    private string GetCombinationKey(List<Guid> team1UserIds, List<Guid> team2UserIds)
    {
        var team1Sorted = string.Join(",", team1UserIds.OrderBy(id => id));
        var team2Sorted = string.Join(",", team2UserIds.OrderBy(id => id));
        var teams = new[] { team1Sorted, team2Sorted }.OrderBy(t => t).ToArray();
        return $"{teams[0]}_vs_{teams[1]}";
    }
}
