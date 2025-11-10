using FifaTracker.Domain.Entities;

namespace FifaTracker.Application.Services;

public class MatchGenerator : IMatchGenerator
{
    public List<Match> GenerateSmartMatches(
        Guid sessionId,
        List<Guid> userIds,
        FifaTracker.Domain.Entities.MatchType matchType,
        int targetCount,
        List<Match> existingMatches,
        Dictionary<Guid, DateTime> userJoinTimes,
        DateTime sessionStartTime)
    {
        var matches = new List<Match>();
        var now = DateTime.UtcNow;
        
        // Filter out unplayed matches from existingMatches
        existingMatches = existingMatches.Where(m => m.IsCompleted).ToList();
        
        // Calculate player priorities based only on completed matches
        var playerStats = CalculatePlayerStats(userIds, existingMatches, userJoinTimes, sessionStartTime, now);
        
        // Generate matches based on type
        for (int i = 0; i < targetCount; i++)
        {
            // Each match gets a slightly later CreatedAt time to maintain order
            var createdAt = now.AddMilliseconds(i);
            
            Match? newMatch = matchType switch
            {
                FifaTracker.Domain.Entities.MatchType.OneVsOne => GenerateSmartOneVsOneMatch(sessionId, playerStats, existingMatches, matches, createdAt),
                FifaTracker.Domain.Entities.MatchType.TwoVsTwo => GenerateSmartTwoVsTwoMatch(sessionId, playerStats, existingMatches, matches, createdAt),
                FifaTracker.Domain.Entities.MatchType.TwoVsOne => GenerateSmartTwoVsOneMatch(sessionId, playerStats, existingMatches, matches, createdAt),
                _ => null
            };
            
            if (newMatch != null)
            {
                matches.Add(newMatch);
                UpdatePlayerStatsAfterMatch(playerStats, newMatch);
            }
            else
            {
                break; // Can't generate more unique matches
            }
        }
        
        return matches;
    }

    private Dictionary<Guid, PlayerMatchStats> CalculatePlayerStats(
        List<Guid> userIds,
        List<Match> existingMatches,
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

    private Match? GenerateSmartOneVsOneMatch(
        Guid sessionId,
        Dictionary<Guid, PlayerMatchStats> playerStats,
        List<Match> existingMatches,
        List<Match> newMatches,
        DateTime createdAt)
    {
        var sortedPlayers = playerStats.OrderByDescending(p => p.Value.Priority).ToList();
        
        if (sortedPlayers.Count < 2)
            return null;
        
        // First pass: Try to find unique matchup
        foreach (var player1 in sortedPlayers)
        {
            foreach (var player2 in sortedPlayers.Where(p => p.Key != player1.Key))
            {
                if (MatchupExists(player1.Key, player2.Key, existingMatches, newMatches))
                    continue;
                
                return CreateMatch(sessionId, new List<Guid> { player1.Key }, new List<Guid> { player2.Key }, createdAt);
            }
        }
        
        // Second pass: If no unique matchup found, shuffle to create variety
        var random = new Random();
        var shuffledPlayers = sortedPlayers.OrderBy(_ => random.Next()).Take(2).ToList();
        return CreateMatch(sessionId, new List<Guid> { shuffledPlayers[0].Key }, new List<Guid> { shuffledPlayers[1].Key }, createdAt);
    }

    private class MatchCombination
    {
        public List<Guid> Team1 { get; set; } = new List<Guid>();
        public List<Guid> Team2 { get; set; } = new List<Guid>();
        public string Key => GetKey(Team1, Team2);

        public static string GetKey(List<Guid> team1, List<Guid> team2)
        {
            var sortedTeam1 = string.Join(",", team1.OrderBy(id => id));
            var sortedTeam2 = string.Join(",", team2.OrderBy(id => id));
            return $"{sortedTeam1}|{sortedTeam2}";
        }
    }

    private Match? GenerateSmartTwoVsTwoMatch(
        Guid sessionId,
        Dictionary<Guid, PlayerMatchStats> playerStats,
        List<Match> existingMatches,
        List<Match> newMatches,
        DateTime createdAt)
    {
        var players = playerStats.Keys.ToList();
        if (players.Count < 4)
            return null;

        // Get all existing match combinations (both from completed and new matches)
        var usedCombinations = new HashSet<string>();
        foreach (var match in existingMatches.Concat(newMatches))
        {
            var team1 = match.MatchTeams.Where(mt => mt.TeamNumber == 1).Select(mt => mt.UserId).ToList();
            var team2 = match.MatchTeams.Where(mt => mt.TeamNumber == 2).Select(mt => mt.UserId).ToList();
            usedCombinations.Add(MatchCombination.GetKey(team1, team2));
        }

        // Generate all possible 2v2 combinations
        var allCombinations = GenerateAllPossibleCombinations(players);

        // First try to find a completely unused combination
        var unusedCombination = allCombinations
            .FirstOrDefault(c => !usedCombinations.Contains(c.Key));

        if (unusedCombination != null)
        {
            return CreateTwoVsTwoMatch(sessionId, unusedCombination.Team1, unusedCombination.Team2, createdAt);
        }

        // If all combinations have been used, find one that hasn't been used in recent matches
        // Get combinations in reverse chronological order (newest first)
        var recentMatchCombinations = existingMatches
            .Concat(newMatches)
            .OrderByDescending(m => m.CreatedAt)
            .Select(m => MatchCombination.GetKey(
                m.MatchTeams.Where(mt => mt.TeamNumber == 1).Select(mt => mt.UserId).ToList(),
                m.MatchTeams.Where(mt => mt.TeamNumber == 2).Select(mt => mt.UserId).ToList()))
            .ToList();

        // Find the combination that was used least recently
        var leastRecentCombination = allCombinations
            .OrderByDescending(c => recentMatchCombinations.IndexOf(c.Key))
            .First();

        return CreateTwoVsTwoMatch(sessionId, leastRecentCombination.Team1, leastRecentCombination.Team2, createdAt);
    }

    private List<MatchCombination> GenerateAllPossibleCombinations(List<Guid> players)
    {
        var combinations = new List<MatchCombination>();
        
        // Generate all possible 2v2 combinations
        for (int i = 0; i < players.Count - 3; i++)
        {
            for (int j = i + 1; j < players.Count - 2; j++)
            {
                for (int k = j + 1; k < players.Count - 1; k++)
                {
                    for (int l = k + 1; l < players.Count; l++)
                    {
                        // Create teams where players i,j are on one team and k,l on another
                        combinations.Add(new MatchCombination 
                        { 
                            Team1 = new List<Guid> { players[i], players[j] },
                            Team2 = new List<Guid> { players[k], players[l] }
                        });

                        // Create teams where players i,k are on one team and j,l on another
                        combinations.Add(new MatchCombination 
                        { 
                            Team1 = new List<Guid> { players[i], players[k] },
                            Team2 = new List<Guid> { players[j], players[l] }
                        });

                        // Create teams where players i,l are on one team and j,k on another
                        combinations.Add(new MatchCombination 
                        { 
                            Team1 = new List<Guid> { players[i], players[l] },
                            Team2 = new List<Guid> { players[j], players[k] }
                        });
                    }
                }
            }
        }

        // Shuffle the combinations to add randomness when all combinations have been used
        var random = new Random();
        return combinations.OrderBy(x => random.Next()).ToList();
    }
    
    private Match CreateTwoVsTwoMatch(Guid sessionId, List<Guid> team1, List<Guid> team2, DateTime createdAt)
    {
        return CreateMatch(sessionId, team1, team2, createdAt);
    }

    private Match? GenerateSmartTwoVsOneMatch(
        Guid sessionId,
        Dictionary<Guid, PlayerMatchStats> playerStats,
        List<Match> existingMatches,
        List<Match> newMatches,
        DateTime createdAt)
    {
        var sortedPlayers = playerStats.OrderByDescending(p => p.Value.Priority).Select(p => p.Key).ToList();
        
        if (sortedPlayers.Count < 3)
            return null;
        
        // First pass: Try to find unique matchup
        for (int solo = 0; solo < sortedPlayers.Count; solo++)
        {
            var soloPlayer = sortedPlayers[solo];
            
            for (int i = 0; i < sortedPlayers.Count - 1; i++)
            {
                if (sortedPlayers[i] == soloPlayer) continue;
                
                for (int j = i + 1; j < sortedPlayers.Count; j++)
                {
                    if (sortedPlayers[j] == soloPlayer) continue;
                    
                    var team = new List<Guid> { sortedPlayers[i], sortedPlayers[j] };
                    
                    if (TwoVsOneMatchupExists(team, soloPlayer, existingMatches, newMatches))
                        continue;
                    
                    return CreateTwoVsOneMatch(sessionId, team, soloPlayer, createdAt);
                }
            }
        }
        
        // Second pass: If no unique matchup found, shuffle to create variety
        if (sortedPlayers.Count >= 3)
        {
            var random = new Random();
            var shuffledPlayers = sortedPlayers.OrderBy(_ => random.Next()).ToList();
            var team = new List<Guid> { shuffledPlayers[0], shuffledPlayers[1] };
            var soloPlayer = shuffledPlayers[2];
            return CreateTwoVsOneMatch(sessionId, team, soloPlayer, createdAt);
        }
        
        return null;
    }
    
    private Match CreateTwoVsOneMatch(Guid sessionId, List<Guid> team, Guid soloPlayer, DateTime createdAt)
    {
        return CreateMatch(sessionId, team, new List<Guid> { soloPlayer }, createdAt);
    }
    
    private Match CreateMatch(Guid sessionId, List<Guid> team1, List<Guid> team2, DateTime createdAt)
    {
        var match = new Match
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            IsGenerated = true,
            IsCompleted = false,
            CreatedAt = createdAt,
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

    private void UpdatePlayerStatsAfterMatch(Dictionary<Guid, PlayerMatchStats> stats, Match match)
    {
        foreach (var mt in match.MatchTeams)
        {
            if (stats.ContainsKey(mt.UserId))
            {
                stats[mt.UserId].PendingMatches++;
                stats[mt.UserId].TotalMatches++;
                stats[mt.UserId].Priority = stats[mt.UserId].ExpectedMatches - stats[mt.UserId].TotalMatches;
            }
        }
    }

    private bool MatchupExists(Guid player1, Guid player2, List<Match> existing, List<Match> newMatches)
    {
        var allMatches = existing.Concat(newMatches);
        return allMatches.Any(m =>
            m.MatchTeams.Count == 2 &&
            m.MatchTeams.Any(mt => mt.UserId == player1) &&
            m.MatchTeams.Any(mt => mt.UserId == player2));
    }

    private bool TeamMatchupExists(List<Guid> team1, List<Guid> team2, List<Match> existing, List<Match> newMatches)
    {
        var allMatches = existing.Concat(newMatches);
        return allMatches.Any(m =>
        {
            var team1Players = m.MatchTeams.Where(mt => mt.TeamNumber == 1).Select(mt => mt.UserId).ToList();
            var team2Players = m.MatchTeams.Where(mt => mt.TeamNumber == 2).Select(mt => mt.UserId).ToList();
            
            // Check if team sizes match first
            if (team1Players.Count != team1.Count || team2Players.Count != team2.Count)
                return false;
            
            // Check if teams match (considering both orientations)
            return (team1.All(id => team1Players.Contains(id)) && team1Players.Count == team1.Count &&
                    team2.All(id => team2Players.Contains(id)) && team2Players.Count == team2.Count) ||
                   (team1.All(id => team2Players.Contains(id)) && team2Players.Count == team1.Count &&
                    team2.All(id => team1Players.Contains(id)) && team1Players.Count == team2.Count);
        });
    }

    private bool TwoVsOneMatchupExists(List<Guid> team, Guid solo, List<Match> existing, List<Match> newMatches)
    {
        var allMatches = existing.Concat(newMatches);
        return allMatches.Any(m =>
        {
            var teamPlayers = m.MatchTeams.Where(mt => mt.TeamNumber == 1).Select(mt => mt.UserId).ToList();
            var soloPlayers = m.MatchTeams.Where(mt => mt.TeamNumber == 2).Select(mt => mt.UserId).ToList();
            
            return team.All(id => teamPlayers.Contains(id)) && soloPlayers.Contains(solo);
        });
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
