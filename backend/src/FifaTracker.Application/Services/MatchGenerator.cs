using FifaTracker.Domain.Entities;
using FifaTracker.Domain.Extensions;

namespace FifaTracker.Application.Services;

public class MatchGenerator : IMatchGenerator
{
    public List<Match> GetPendingMatches(
        Guid sessionId,
        List<Guid> userIds,
        List<SessionUser> sessionUsers,
        Domain.Entities.MatchType matchType,
        List<Match> completedMatches,
        DateTime sessionStartTime)
    {
        // Generate ALL possible combinations - DON'T filter completed ones
        var allCombinations = GenerateAllPossibleCombinations(sessionId, userIds, matchType);
        
        // Calculate priority for each match (including already played ones)
        foreach (var match in allCombinations)
        {
            match.Priority = match.IsGenerated 
                ? CalculateMatchPriority(match, sessionUsers, completedMatches, allCombinations, sessionStartTime)
                : double.MaxValue; // Custom matches always first
        }
        
        // Sort: custom matches first, then by priority descending
        // This returns ALL combinations (including completed ones) sorted by priority
        return allCombinations
            .OrderByDescending(m => m.Priority)
            .ToList();
    }

    public List<Match> GenerateAllPossibleCombinations(
    Guid sessionId,
    List<Guid> userIds,
    Domain.Entities.MatchType matchType)
    {
        return matchType switch
        {
            Domain.Entities.MatchType.OneVsOne => GenerateAllOneVsOneCombinations(sessionId, userIds),
            Domain.Entities.MatchType.TwoVsTwo => GenerateAllTwoVsTwoCombinations(sessionId, userIds),
            Domain.Entities.MatchType.TwoVsOne => GenerateAllTwoVsOneCombinations(sessionId, userIds),
            _ => []
        };
    }

    public double CalculateMatchPriority(
        Match match,
        List<SessionUser> sessionUsers,
        List<Match> completedMatches,
        List<Match> allPossibleCombinations,
        DateTime sessionStartTime)
    {
        var now = DateTime.UtcNow;
        var playerIds = match.MatchTeams.Select(mt => mt.UserId).ToList();
        
        // Check if this specific match was already played
        var wasPlayed = MatchExistsInCompleted(match, completedMatches);
        
        // Check if ALL possible combinations have been played
        var allUserIds = sessionUsers.Select(su => su.UserId).ToList();
        var matchType = GetMatchTypeFromTeamSizes(match);

        if (allPossibleCombinations == null || allPossibleCombinations.Count == 0)
            allPossibleCombinations = GenerateAllPossibleCombinations(match.SessionId, allUserIds, matchType);

        var allPlayed = allPossibleCombinations.All(combo => MatchExistsInCompleted(combo, completedMatches));
        
        // If this match was played AND not all combinations were played yet, apply penalty
        if (wasPlayed && !allPlayed)
        {
            return -1000.0;
        }
        
        // If all combinations were played, reset - no penalty for played matches
        // Calculate normal priority based on fairness
        var fairnessScore = 0.0;
        var timeBonus = 0.0;
        
        foreach (var playerId in playerIds)
        {
            var sessionUser = sessionUsers.FirstOrDefault(su => su.UserId == playerId);
            if (sessionUser == null) continue;
            
            var activeTime = sessionUser.GetCurrentActiveTotalHours(now);
            var playerCompletedCount = completedMatches.Count(m => m.MatchTeams.Any(mt => mt.UserId == playerId));
            
            // Simple fairness: players with fewer completed matches should play more
            var avgMatchesPerPlayer = completedMatches.Count > 0 
                ? completedMatches.SelectMany(m => m.MatchTeams).Count() / (double)sessionUsers.Count
                : 0;
            
            fairnessScore += (avgMatchesPerPlayer - playerCompletedCount);
            timeBonus += activeTime; // Players with more active time get slight bonus
        }
        
        // Penalty for repetition: if any player was in the last completed match
        var repetitionPenalty = 0.0;
        var lastMatch = completedMatches.OrderByDescending(m => m.CreatedAt).FirstOrDefault();
        if (lastMatch != null)
        {
            var lastMatchPlayerIds = lastMatch.MatchTeams.Select(mt => mt.UserId).ToList();
            var repeatCount = playerIds.Count(pid => lastMatchPlayerIds.Contains(pid));
            repetitionPenalty = repeatCount * 10.0; // -10 per repeated player
        }
        
        // Final priority: fairness (most important) + time bonus - repetition penalty
        return (fairnessScore * 100.0) + (timeBonus * 5.0) - repetitionPenalty;
    }
    
    private Domain.Entities.MatchType GetMatchTypeFromTeamSizes(Match match)
    {
        var team1Size = match.MatchTeams.Count(mt => mt.TeamNumber == 1);
        var team2Size = match.MatchTeams.Count(mt => mt.TeamNumber == 2);
        
        if (team1Size == 1 && team2Size == 1) return Domain.Entities.MatchType.OneVsOne;
        if (team1Size == 2 && team2Size == 2) return Domain.Entities.MatchType.TwoVsTwo;
        if (team1Size == 2 && team2Size == 1) return Domain.Entities.MatchType.TwoVsOne;
        if (team1Size == 1 && team2Size == 2) return Domain.Entities.MatchType.TwoVsOne;
        
        return Domain.Entities.MatchType.OneVsOne; // Default fallback
    }

    private List<Match> GenerateAllOneVsOneCombinations(Guid sessionId, List<Guid> userIds)
    {
        var matches = new List<Match>();
        if (userIds.Count < 2) return matches;
        
        for (int i = 0; i < userIds.Count - 1; i++)
        {
            for (int j = i + 1; j < userIds.Count; j++)
            {
                matches.Add(CreateMatch(sessionId, [userIds[i]], [userIds[j]]));
            }
        }
        
        return matches;
    }

    private List<Match> GenerateAllTwoVsTwoCombinations(Guid sessionId, List<Guid> userIds)
    {
        var matches = new List<Match>();
        if (userIds.Count < 4) return matches;
        
        var players = userIds.ToArray();
        var n = players.Length;
        
        // Generate all 2-player teams: C(n,2)
        var teams = new List<List<Guid>>();
        for (int i = 0; i < n - 1; i++)
        {
            for (int j = i + 1; j < n; j++)
            {
                teams.Add(new List<Guid> { players[i], players[j] });
            }
        }
        
        // Find all pairs of non-overlapping teams and track unique matches
        var uniqueMatches = new HashSet<string>();
        
        for (int i = 0; i < teams.Count - 1; i++)
        {
            for (int j = i + 1; j < teams.Count; j++)
            {
                var team1 = teams[i];
                var team2 = teams[j];
                
                // Check if teams don't share players
                if (!team1.Any(p => team2.Contains(p)))
                {
                    // Create normalized key: sort teams by first player, then create ordered key
                    var sortedTeams = new[] { team1, team2 }
                        .OrderBy(t => t[0])
                        .ThenBy(t => t[1])
                        .ToList();
                    
                    var key = $"{sortedTeams[0][0]},{sortedTeams[0][1]}_vs_{sortedTeams[1][0]},{sortedTeams[1][1]}";
                    
                    if (uniqueMatches.Add(key))
                    {
                        matches.Add(CreateMatch(sessionId, team1, team2));
                    }
                }
            }
        }
        
        return matches;
    }

    private List<Match> GenerateAllTwoVsOneCombinations(Guid sessionId, List<Guid> userIds)
    {
        var matches = new List<Match>();
        if (userIds.Count < 3) return matches;
        
        for (int solo = 0; solo < userIds.Count; solo++)
        {
            for (int i = 0; i < userIds.Count - 1; i++)
            {
                if (i == solo) continue;
                
                for (int j = i + 1; j < userIds.Count; j++)
                {
                    if (j == solo) continue;
                    
                    var team = new List<Guid> { userIds[i], userIds[j] };
                    matches.Add(CreateMatch(sessionId, team, [userIds[solo]]));
                }
            }
        }
        
        return matches;
    }

    private bool MatchExistsInCompleted(Match candidateMatch, List<Match> completedMatches)
    {
        var candidateTeam1 = candidateMatch.MatchTeams.Where(mt => mt.TeamNumber == 1).Select(mt => mt.UserId).OrderBy(id => id).ToList();
        var candidateTeam2 = candidateMatch.MatchTeams.Where(mt => mt.TeamNumber == 2).Select(mt => mt.UserId).OrderBy(id => id).ToList();
        
        return completedMatches.Any(m =>
        {
            var team1 = m.MatchTeams.Where(mt => mt.TeamNumber == 1).Select(mt => mt.UserId).OrderBy(id => id).ToList();
            var team2 = m.MatchTeams.Where(mt => mt.TeamNumber == 2).Select(mt => mt.UserId).OrderBy(id => id).ToList();
            
            // Check both orientations: team1 vs team2 OR team2 vs team1
            return (team1.SequenceEqual(candidateTeam1) && team2.SequenceEqual(candidateTeam2)) ||
                   (team1.SequenceEqual(candidateTeam2) && team2.SequenceEqual(candidateTeam1));
        });
    }

    private Match CreateMatch(Guid sessionId, List<Guid> team1, List<Guid> team2)
    {
        var match = new Match
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            IsGenerated = true,
            IsCompleted = false,
            CreatedAt = DateTime.UtcNow,
            MatchTeams = new List<MatchTeam>()
        };
        
        foreach (var userId in team1)
        {
            match.MatchTeams.Add(new MatchTeam
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                TeamNumber = 1
            });
        }
        
        foreach (var userId in team2)
        {
            match.MatchTeams.Add(new MatchTeam
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                TeamNumber = 2
            });
        }
        
        return match;
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
        public List<Guid> RecentTeammates { get; set; } = new();
        public List<Guid> RecentOpponents { get; set; } = new();
    }
}
