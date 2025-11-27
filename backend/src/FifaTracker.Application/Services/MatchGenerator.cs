using FifaTracker.Domain.Entities;
using FifaTracker.Domain.Extensions;

namespace FifaTracker.Application.Services;

public class MatchGenerator : IMatchGenerator
{
    private static readonly Random Random = new();

    public List<Match> GenerateSmartMatches(
        Guid sessionId,
        List<Guid> userIds,
        List<SessionUser> sessionUsers,
        FifaTracker.Domain.Entities.MatchType matchType,
        int targetCount,
        List<Match> existingMatches,
        DateTime sessionStartTime)
    {
        var matches = new List<Match>();
        var now = DateTime.UtcNow;
        
        // Only use completed matches - pending matches are temporary and will be regenerated
        var completedMatches = existingMatches.Where(m => m.IsCompleted).ToList();
        var playerStats = CalculatePlayerStats(userIds, sessionUsers, completedMatches, sessionStartTime, now);
        
        for (int i = 0; i < targetCount; i++)
        {
            var createdAt = now.AddMilliseconds(i);
            
            Match? newMatch = matchType switch
            {
                FifaTracker.Domain.Entities.MatchType.OneVsOne => GenerateSmartOneVsOneMatch(sessionId, playerStats, completedMatches, matches, createdAt),
                FifaTracker.Domain.Entities.MatchType.TwoVsTwo => GenerateSmartTwoVsTwoMatch(sessionId, playerStats, completedMatches, matches, createdAt),
                FifaTracker.Domain.Entities.MatchType.TwoVsOne => GenerateSmartTwoVsOneMatch(sessionId, playerStats, completedMatches, matches, createdAt),
                _ => null
            };
            
            if (newMatch == null) break;
            
            matches.Add(newMatch);
            UpdatePlayerStatsAfterMatch(playerStats, newMatch);
        }
        
        return matches;
    }

    private Dictionary<Guid, PlayerMatchStats> CalculatePlayerStats(
        List<Guid> userIds,
        List<SessionUser> sessionUsers,
        List<Match> completedMatches,
        DateTime sessionStartTime,
        DateTime now)
    {
        var stats = new Dictionary<Guid, PlayerMatchStats>();
        var matchesPerHour = CalculateMatchesPerHour(completedMatches, sessionUsers, now);
        var sessionDuration = (now - sessionStartTime).TotalHours;
        
        // If no completed matches yet, estimate based on session duration
        if (matchesPerHour == 0 && completedMatches.Count == 0)
        {
            // Assume ~2 matches per hour per player as default
            matchesPerHour = 2.0;
        }
        
        foreach (var userId in userIds)
        {
            var sessionUser = sessionUsers.FirstOrDefault(su => su.UserId == userId);
            if (sessionUser == null) continue;
            
            var activeTime = sessionUser.GetCurrentActiveTotalHours(now);
            var playerCompletedMatches = completedMatches.Where(m => m.MatchTeams.Any(mt => mt.UserId == userId)).ToList();
            var completedCount = playerCompletedMatches.Count;
            
            var expectedMatches = activeTime * matchesPerHour;
            
            stats[userId] = new PlayerMatchStats
            {
                UserId = userId,
                TotalMatches = completedCount, // Only completed matches count
                CompletedMatches = completedCount,
                PendingMatches = 0, // Pending matches are ignored
                TimeInSession = activeTime,
                TimeRatio = sessionDuration > 0 ? activeTime / sessionDuration : 1.0,
                ExpectedMatches = expectedMatches,
                Priority = expectedMatches - completedCount, // Priority based only on completed matches
                Teammates = new HashSet<Guid>(),
                Opponents = new HashSet<Guid>(),
                RecentTeammates = new List<Guid>(),
                RecentOpponents = new List<Guid>()
            };
            
            TrackPlayerRelationships(stats[userId], playerCompletedMatches, userId);
        }
        
        return stats;
    }

    private double CalculateMatchesPerHour(List<Match> completedMatches, List<SessionUser> sessionUsers, DateTime now)
    {
        if (completedMatches.Count == 0) return 0;
        
        var totalActiveHours = sessionUsers
            .Where(su => completedMatches.Any(m => m.MatchTeams.Any(mt => mt.UserId == su.UserId)))
            .Sum(su => su.GetCurrentActiveTotalHours(now));
        
        var totalMatchesPlayed = completedMatches.SelectMany(m => m.MatchTeams).Count();
        
        return totalActiveHours > 0 ? totalMatchesPlayed / totalActiveHours : 0;
    }

    private void TrackPlayerRelationships(PlayerMatchStats stats, List<Match> playerMatches, Guid userId)
    {
        var recentMatches = playerMatches.OrderByDescending(m => m.CreatedAt).Take(5).ToList();
        
        foreach (var match in playerMatches)
        {
            var playerTeamNumber = match.MatchTeams.First(mt => mt.UserId == userId).TeamNumber;
            var isRecent = recentMatches.Contains(match);
            
            foreach (var mt in match.MatchTeams.Where(mt => mt.UserId != userId))
            {
                if (mt.TeamNumber == playerTeamNumber)
                {
                    stats.Teammates.Add(mt.UserId);
                    if (isRecent) stats.RecentTeammates.Add(mt.UserId);
                }
                else
                {
                    stats.Opponents.Add(mt.UserId);
                    if (isRecent) stats.RecentOpponents.Add(mt.UserId);
                }
            }
        }
    }

    private Match? GenerateSmartOneVsOneMatch(
        Guid sessionId,
        Dictionary<Guid, PlayerMatchStats> playerStats,
        List<Match> completedMatches,
        List<Match> newMatches,
        DateTime createdAt)
    {
        var sortedPlayers = playerStats.OrderByDescending(p => p.Value.Priority).Select(p => p.Key).ToList();
        if (sortedPlayers.Count < 2) return null;
        
        foreach (var player1 in sortedPlayers)
        {
            foreach (var player2 in sortedPlayers.Where(p => p != player1))
            {
                if (!MatchupExists(player1, player2, completedMatches, newMatches))
                    return CreateMatch(sessionId, [player1], [player2], createdAt);
            }
        }
        
        var shuffled = sortedPlayers.OrderBy(_ => Random.Next()).Take(2).ToList();
        return CreateMatch(sessionId, [shuffled[0]], [shuffled[1]], createdAt);
    }

    private Match? GenerateSmartTwoVsTwoMatch(
        Guid sessionId,
        Dictionary<Guid, PlayerMatchStats> playerStats,
        List<Match> completedMatches,
        List<Match> newMatches,
        DateTime createdAt)
    {
        var sortedPlayers = playerStats.OrderByDescending(p => p.Value.Priority).Select(p => p.Key).ToList();
        if (sortedPlayers.Count < 4) return null;
        
        var bestMatch = (Match?)null;
        var bestScore = double.MinValue;
        
        for (int i = 0; i < sortedPlayers.Count - 3; i++)
        {
            for (int j = i + 1; j < sortedPlayers.Count - 2; j++)
            {
                var team1 = new List<Guid> { sortedPlayers[i], sortedPlayers[j] };
                
                for (int k = 0; k < sortedPlayers.Count - 1; k++)
                {
                    if (team1.Contains(sortedPlayers[k])) continue;
                    
                    for (int l = k + 1; l < sortedPlayers.Count; l++)
                    {
                        if (team1.Contains(sortedPlayers[l])) continue;
                        
                        var team2 = new List<Guid> { sortedPlayers[k], sortedPlayers[l] };
                        
                        if (!TeamMatchupExists(team1, team2, completedMatches, newMatches))
                        {
                            var score = CalculateMatchupDiversityScore(team1, team2, playerStats);
                            if (score > bestScore)
                            {
                                bestScore = score;
                                bestMatch = CreateMatch(sessionId, team1, team2, createdAt);
                            }
                        }
                    }
                }
            }
        }
        
        if (bestMatch != null) return bestMatch;
        
        var shuffled = sortedPlayers.OrderBy(_ => Random.Next()).ToList();
        return CreateMatch(sessionId, [shuffled[0], shuffled[1]], [shuffled[2], shuffled[3]], createdAt);
    }

    private Match? GenerateSmartTwoVsOneMatch(
        Guid sessionId,
        Dictionary<Guid, PlayerMatchStats> playerStats,
        List<Match> completedMatches,
        List<Match> newMatches,
        DateTime createdAt)
    {
        var sortedPlayers = playerStats.OrderByDescending(p => p.Value.Priority).Select(p => p.Key).ToList();
        if (sortedPlayers.Count < 3) return null;
        
        var bestMatch = (Match?)null;
        var bestScore = double.MinValue;
        
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
                    
                    if (!TwoVsOneMatchupExists(team, soloPlayer, completedMatches, newMatches))
                    {
                        var score = CalculateMatchupDiversityScore(team, [soloPlayer], playerStats);
                        if (score > bestScore)
                        {
                            bestScore = score;
                            bestMatch = CreateMatch(sessionId, team, [soloPlayer], createdAt);
                        }
                    }
                }
            }
        }
        
        if (bestMatch != null) return bestMatch;
        
        var shuffled = sortedPlayers.OrderBy(_ => Random.Next()).ToList();
        return CreateMatch(sessionId, [shuffled[0], shuffled[1]], [shuffled[2]], createdAt);
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
        // When generating new matches in batch, temporarily track them as "pending"
        // but these don't affect priority calculation in the next generation cycle
        foreach (var mt in match.MatchTeams.Where(mt => stats.ContainsKey(mt.UserId)))
        {
            stats[mt.UserId].PendingMatches++;
            stats[mt.UserId].TotalMatches++;
            stats[mt.UserId].Priority = stats[mt.UserId].ExpectedMatches - stats[mt.UserId].TotalMatches;
            
            var playerTeamNumber = mt.TeamNumber;
            foreach (var otherMt in match.MatchTeams.Where(other => other.UserId != mt.UserId))
            {
                if (otherMt.TeamNumber == playerTeamNumber)
                {
                    stats[mt.UserId].RecentTeammates.Add(otherMt.UserId);
                    if (stats[mt.UserId].RecentTeammates.Count > 10)
                        stats[mt.UserId].RecentTeammates.RemoveAt(0);
                }
                else
                {
                    stats[mt.UserId].RecentOpponents.Add(otherMt.UserId);
                    if (stats[mt.UserId].RecentOpponents.Count > 10)
                        stats[mt.UserId].RecentOpponents.RemoveAt(0);
                }
            }
        }
    }
    
    private double CalculateMatchupDiversityScore(List<Guid> team1, List<Guid> team2, Dictionary<Guid, PlayerMatchStats> stats)
    {
        double diversityScore = 0;
        double priorityScore = 0;
        var allPlayers = team1.Concat(team2).ToList();
        
        // Calculate diversity score
        foreach (var player in allPlayers)
        {
            if (!stats.ContainsKey(player)) continue;
            var playerStats = stats[player];
            
            // Add priority bonus to ensure high-priority players get selected
            priorityScore += playerStats.Priority;
            
            foreach (var teammate in team1.Contains(player) ? team1 : team2)
            {
                if (teammate == player) continue;
                if (!playerStats.RecentTeammates.Contains(teammate)) diversityScore += 2;
                else diversityScore -= playerStats.RecentTeammates.Count(t => t == teammate);
            }
            
            foreach (var opponent in team1.Contains(player) ? team2 : team1)
            {
                if (!playerStats.RecentOpponents.Contains(opponent)) diversityScore += 1;
                else diversityScore -= playerStats.RecentOpponents.Count(o => o == opponent) * 0.5;
            }
        }
        
        // Weight priority significantly higher than diversity (5x multiplier)
        return diversityScore + (priorityScore * 5.0);
    }

    private bool MatchupExists(Guid player1, Guid player2, List<Match> completed, List<Match> newMatches)
    {
        var allMatches = completed.Concat(newMatches);
        return allMatches.Any(m =>
            m.MatchTeams.Count == 2 &&
            m.MatchTeams.Any(mt => mt.UserId == player1) &&
            m.MatchTeams.Any(mt => mt.UserId == player2));
    }

    private bool TeamMatchupExists(List<Guid> team1, List<Guid> team2, List<Match> completed, List<Match> newMatches)
    {
        var allMatches = completed.Concat(newMatches);
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

    private bool TwoVsOneMatchupExists(List<Guid> team, Guid solo, List<Match> completed, List<Match> newMatches)
    {
        var allMatches = completed.Concat(newMatches);
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
        public List<Guid> RecentTeammates { get; set; } = new();
        public List<Guid> RecentOpponents { get; set; } = new();
    }
}
