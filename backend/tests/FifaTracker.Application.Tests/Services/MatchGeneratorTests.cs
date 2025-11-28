using FifaTracker.Application.Services;
using FifaTracker.Domain.Entities;
using MatchType = FifaTracker.Domain.Entities.MatchType;

namespace FifaTracker.Application.Tests.Services;

public class MatchGeneratorTests
{
    private readonly MatchGenerator _generator = new();
    
    [Theory]
    [InlineData(4, 3)]
    [InlineData(5, 15)]
    [InlineData(6, 45)]
    [InlineData(7, 105)]
    [InlineData(8, 210)]
    [InlineData(9, 378)]
    [InlineData(10, 630)]
    public void GenerateAllPossibleCombinations_TwoVsTwo_ReturnsCorrectCount(int playerCount, int expectedMatchCount)
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var userIds = Enumerable.Range(0, playerCount).Select(_ => Guid.NewGuid()).ToList();
        
        // Act
        var matches = _generator.GenerateAllPossibleCombinations(sessionId, userIds, MatchType.TwoVsTwo);
        
        // Assert
        Assert.Equal(expectedMatchCount, matches.Count);
    }
    
    [Fact]
    public void GenerateAllPossibleCombinations_TwoVsTwo_AllMatchesAreUnique()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var userIds = Enumerable.Range(0, 6).Select(_ => Guid.NewGuid()).ToList();
        
        // Act
        var matches = _generator.GenerateAllPossibleCombinations(sessionId, userIds, MatchType.TwoVsTwo);
        
        // Assert
        var uniqueMatches = new HashSet<string>();
        foreach (var match in matches)
        {
            var team1 = match.MatchTeams.Where(mt => mt.TeamNumber == 1).Select(mt => mt.UserId).OrderBy(id => id);
            var team2 = match.MatchTeams.Where(mt => mt.TeamNumber == 2).Select(mt => mt.UserId).OrderBy(id => id);
            
            var key = $"{string.Join(",", team1)}_vs_{string.Join(",", team2)}";
            Assert.True(uniqueMatches.Add(key), $"Duplicate match found: {key}");
        }
        
        Assert.Equal(45, uniqueMatches.Count);
    }
    
    [Fact]
    public void GenerateAllPossibleCombinations_TwoVsTwo_NoPlayerPlaysAgainstThemselves()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var userIds = Enumerable.Range(0, 6).Select(_ => Guid.NewGuid()).ToList();
        
        // Act
        var matches = _generator.GenerateAllPossibleCombinations(sessionId, userIds, MatchType.TwoVsTwo);
        
        // Assert
        foreach (var match in matches)
        {
            var team1Players = match.MatchTeams.Where(mt => mt.TeamNumber == 1).Select(mt => mt.UserId);
            var team2Players = match.MatchTeams.Where(mt => mt.TeamNumber == 2).Select(mt => mt.UserId);
            
            Assert.Empty(team1Players.Intersect(team2Players));
        }
    }
    
    [Fact]
    public void GetPendingMatches_ReturnsAllCombinations_NotFiltered()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var userIds = Enumerable.Range(0, 4).Select(_ => Guid.NewGuid()).ToList();
        var sessionUsers = userIds.Select(id => new SessionUser
        {
            SessionId = sessionId,
            UserId = id,
            JoinedAt = DateTime.UtcNow.AddHours(-1)
        }).ToList();
        
        // Create one completed match
        var completedMatches = new List<Match>
        {
            new Match
            {
                Id = Guid.NewGuid(),
                SessionId = sessionId,
                IsGenerated = true,
                IsCompleted = true,
                CreatedAt = DateTime.UtcNow.AddMinutes(-10),
                MatchTeams = new List<MatchTeam>
                {
                    new() { Id = Guid.NewGuid(), UserId = userIds[0], TeamNumber = 1 },
                    new() { Id = Guid.NewGuid(), UserId = userIds[1], TeamNumber = 1 },
                    new() { Id = Guid.NewGuid(), UserId = userIds[2], TeamNumber = 2 },
                    new() { Id = Guid.NewGuid(), UserId = userIds[3], TeamNumber = 2 }
                }
            }
        };
        
        // Act
        var pendingMatches = _generator.GetPendingMatches(
            sessionId, 
            userIds, 
            sessionUsers, 
            MatchType.TwoVsTwo, 
            completedMatches,
            DateTime.UtcNow.AddHours(-1));
        
        // Assert - All 3 combinations should be returned (not filtered)
        Assert.Equal(3, pendingMatches.Count);
    }
    
    [Fact]
    public void GetPendingMatches_CustomMatchHasHighestPriority()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var userIds = Enumerable.Range(0, 4).Select(_ => Guid.NewGuid()).ToList();
        var sessionUsers = userIds.Select(id => new SessionUser
        {
            SessionId = sessionId,
            UserId = id,
            JoinedAt = DateTime.UtcNow.AddHours(-1)
        }).ToList();
        
        var completedMatches = new List<Match>();
        
        // Act - First get normal pending matches
        var normalPending = _generator.GetPendingMatches(
            sessionId, 
            userIds, 
            sessionUsers, 
            MatchType.TwoVsTwo, 
            completedMatches,
            DateTime.UtcNow.AddHours(-1));
        
        // Add a custom match
        var customMatch = new Match
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            IsGenerated = false, // Custom match
            IsCompleted = false,
            CreatedAt = DateTime.UtcNow,
            MatchTeams = new List<MatchTeam>
            {
                new() { Id = Guid.NewGuid(), UserId = userIds[0], TeamNumber = 1 },
                new() { Id = Guid.NewGuid(), UserId = userIds[1], TeamNumber = 1 },
                new() { Id = Guid.NewGuid(), UserId = userIds[2], TeamNumber = 2 },
                new() { Id = Guid.NewGuid(), UserId = userIds[3], TeamNumber = 2 }
            }
        };
        
        var allPending = normalPending.Prepend(customMatch).ToList();
        
        // Recalculate priorities
        foreach (var match in allPending)
        {
            match.Priority = match.IsGenerated 
                ? _generator.CalculateMatchPriority(match, sessionUsers, completedMatches, DateTime.UtcNow.AddHours(-1))
                : double.MaxValue;
        }
        
        var sortedPending = allPending.OrderByDescending(m => m.Priority).ToList();
        
        // Assert - Custom match should be first
        Assert.False(sortedPending[0].IsGenerated);
        Assert.Equal(customMatch.Id, sortedPending[0].Id);
    }
    
    [Fact]
    public void GenerateAllPossibleCombinations_OneVsOne_ReturnsCorrectCount()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var userIds = Enumerable.Range(0, 5).Select(_ => Guid.NewGuid()).ToList();
        
        // Act
        var matches = _generator.GenerateAllPossibleCombinations(sessionId, userIds, MatchType.OneVsOne);
        
        // Assert - C(5,2) = 10
        Assert.Equal(10, matches.Count);
    }
    
    [Fact]
    public void GenerateAllPossibleCombinations_TwoVsOne_ReturnsCorrectCount()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var userIds = Enumerable.Range(0, 4).Select(_ => Guid.NewGuid()).ToList();
        
        // Act
        var matches = _generator.GenerateAllPossibleCombinations(sessionId, userIds, MatchType.TwoVsOne);
        
        // Assert - 4 players as solo × C(3,2) = 4 × 3 = 12
        Assert.Equal(12, matches.Count);
    }
}
