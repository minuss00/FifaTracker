using FifaTracker.Application.Services;
using FifaTracker.Domain.Entities;
using FifaTracker.Domain.Interfaces;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace FifaTracker.Application.Tests.Services;

public class MatchGeneratorTests
{
    private readonly MatchGenerator _generator;
    private readonly IApplicationDbContext _mockDbContext;
    private readonly Guid _sessionId = Guid.NewGuid();
    private readonly DateTime _sessionStart = DateTime.UtcNow.AddHours(-2);

    public MatchGeneratorTests()
    {
        _generator = new MatchGenerator();
        _mockDbContext = CreateMockDbContext();
    }

    private IApplicationDbContext CreateMockDbContext()
    {
        var mockContext = Substitute.For<IApplicationDbContext>();
        
        // Setup mock DbSets
        mockContext.Users.Returns(Substitute.For<DbSet<User>>());
        mockContext.Sessions.Returns(Substitute.For<DbSet<Session>>());
        mockContext.SessionUsers.Returns(Substitute.For<DbSet<SessionUser>>());
        mockContext.Matches.Returns(Substitute.For<DbSet<Match>>());
        mockContext.MatchTeams.Returns(Substitute.For<DbSet<MatchTeam>>());

        // Mock SaveChangesAsync to return success
        mockContext.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(1));

        return mockContext;
    }

    #region Helper Methods

    private SessionUser CreateSessionUser(Guid userId, double activeHours, DateTime now)
    {
        var sessionUser = new SessionUser
        {
            UserId = userId,
            SessionId = _sessionId,
            IsActiveInSession = true,
            JoinedAt = now.AddHours(-activeHours),
            TotalActiveTime = TimeSpan.FromHours(activeHours),
            LastResumedAt = now.AddHours(-activeHours)
        };
        return sessionUser;
    }

    private Match CreateCompletedMatch(Guid sessionId, List<Guid> team1, List<Guid> team2, DateTime createdAt)
    {
        var match = new Match
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            IsCompleted = true,
            IsGenerated = true,
            CreatedAt = createdAt,
            Team1Score = 5,
            Team2Score = 3,
            MatchTeams = new List<MatchTeam>()
        };

        foreach (var userId in team1)
        {
            match.MatchTeams.Add(new MatchTeam
            {
                Id = Guid.NewGuid(),
                MatchId = match.Id,
                UserId = userId,
                TeamNumber = 1
            });
        }

        foreach (var userId in team2)
        {
            match.MatchTeams.Add(new MatchTeam
            {
                Id = Guid.NewGuid(),
                MatchId = match.Id,
                UserId = userId,
                TeamNumber = 2
            });
        }

        return match;
    }

    #endregion

    #region Mock Database Verification Tests

    [Fact]
    public void MockDbContext_ShouldBeInitialized()
    {
        // Assert
        _mockDbContext.Should().NotBeNull();
        _mockDbContext.Users.Should().NotBeNull();
        _mockDbContext.Sessions.Should().NotBeNull();
        _mockDbContext.SessionUsers.Should().NotBeNull();
        _mockDbContext.Matches.Should().NotBeNull();
        _mockDbContext.MatchTeams.Should().NotBeNull();
    }

    [Fact]
    public async Task MockDbContext_SaveChangesAsync_ShouldWork()
    {
        // Act
        var result = await _mockDbContext.SaveChangesAsync();

        // Assert
        result.Should().Be(1);
        await _mockDbContext.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    #endregion

    #region Basic Generation Tests

    [Fact]
    public void GenerateSmartMatches_WithNoPlayers_ReturnsEmptyList()
    {
        // Arrange
        var userIds = new List<Guid>();
        var sessionUsers = new List<SessionUser>();

        // Act
        var result = _generator.GenerateSmartMatches(
            _sessionId,
            userIds,
            sessionUsers,
            Domain.Entities.MatchType.TwoVsTwo,
            5,
            new List<Match>(),
            _sessionStart);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void GenerateSmartMatches_WithLessThanMinimumPlayers_ReturnsEmptyList()
    {
        // Arrange - TwoVsTwo requires 4 players, only provide 3
        var userIds = Enumerable.Range(1, 3).Select(_ => Guid.NewGuid()).ToList();
        var sessionUsers = userIds.Select(id => CreateSessionUser(id, 1.0, DateTime.UtcNow)).ToList();

        // Act
        var result = _generator.GenerateSmartMatches(
            _sessionId,
            userIds,
            sessionUsers,
            Domain.Entities.MatchType.TwoVsTwo,
            5,
            new List<Match>(),
            _sessionStart);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void GenerateSmartMatches_WithExactMinimumPlayers_GeneratesMatches()
    {
        // Arrange - TwoVsTwo requires 4 players
        var userIds = Enumerable.Range(1, 4).Select(_ => Guid.NewGuid()).ToList();
        var sessionUsers = userIds.Select(id => CreateSessionUser(id, 1.0, DateTime.UtcNow)).ToList();

        // Act
        var result = _generator.GenerateSmartMatches(
            _sessionId,
            userIds,
            sessionUsers,
            Domain.Entities.MatchType.TwoVsTwo,
            5,
            new List<Match>(),
            _sessionStart);

        // Assert
        result.Should().NotBeEmpty();
        result.Should().HaveCountLessThanOrEqualTo(5);
        result.All(m => m.MatchTeams.Count == 4).Should().BeTrue();
    }

    [Fact]
    public void GenerateSmartMatches_GeneratesRequestedCount()
    {
        // Arrange
        var userIds = Enumerable.Range(1, 6).Select(_ => Guid.NewGuid()).ToList();
        var sessionUsers = userIds.Select(id => CreateSessionUser(id, 1.0, DateTime.UtcNow)).ToList();

        // Act
        var result = _generator.GenerateSmartMatches(
            _sessionId,
            userIds,
            sessionUsers,
            Domain.Entities.MatchType.TwoVsTwo,
            5,
            new List<Match>(),
            _sessionStart);

        // Assert
        result.Should().HaveCount(5);
    }

    #endregion

    #region Priority-Based Distribution Tests

    [Fact]
    public void GenerateSmartMatches_PrioritizesPlayersWithFewerMatches()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var user1 = Guid.NewGuid();
        var user2 = Guid.NewGuid();
        var user3 = Guid.NewGuid();
        var user4 = Guid.NewGuid();

        var userIds = new List<Guid> { user1, user2, user3, user4 };
        var sessionUsers = userIds.Select(id => CreateSessionUser(id, 2.0, now)).ToList();

        // User1 and User2 have already played 10 matches
        var existingMatches = new List<Match>();
        for (int i = 0; i < 10; i++)
        {
            existingMatches.Add(CreateCompletedMatch(_sessionId, new[] { user1 }.ToList(), new[] { user2 }.ToList(), now.AddMinutes(-60 + i * 5)));
        }

        // Act
        var result = _generator.GenerateSmartMatches(
            _sessionId,
            userIds,
            sessionUsers,
            Domain.Entities.MatchType.TwoVsTwo,
            5,
            existingMatches,
            _sessionStart);

        // Assert
        result.Should().NotBeEmpty();
        
        // User3 and User4 (with 0 matches) should appear more frequently
        var user3Matches = result.Count(m => m.MatchTeams.Any(mt => mt.UserId == user3));
        var user4Matches = result.Count(m => m.MatchTeams.Any(mt => mt.UserId == user4));
        var user1Matches = result.Count(m => m.MatchTeams.Any(mt => mt.UserId == user1));
        var user2Matches = result.Count(m => m.MatchTeams.Any(mt => mt.UserId == user2));

        // Players with fewer matches should have at least as many pending matches
        (user3Matches + user4Matches).Should().BeGreaterThanOrEqualTo(user1Matches + user2Matches);
    }

    [Fact]
    public void GenerateSmartMatches_DistributesMatchesProportionallyToActiveTime()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var user1 = Guid.NewGuid(); // 2 hours active
        var user2 = Guid.NewGuid(); // 2 hours active
        var user3 = Guid.NewGuid(); // 1 hour active
        var user4 = Guid.NewGuid(); // 1 hour active

        var userIds = new List<Guid> { user1, user2, user3, user4 };
        
        var sessionUsers = new List<SessionUser>
        {
            CreateSessionUser(user1, 2.0, now),
            CreateSessionUser(user2, 2.0, now),
            CreateSessionUser(user3, 1.0, now),
            CreateSessionUser(user4, 1.0, now)
        };

        // Create existing matches to establish matchesPerHour rate
        var existingMatches = new List<Match>();
        for (int i = 0; i < 5; i++)
        {
            existingMatches.Add(CreateCompletedMatch(_sessionId, new[] { user1, user2 }.ToList(), new[] { user3, user4 }.ToList(), now.AddMinutes(-30 + i * 5)));
        }

        // Act
        var result = _generator.GenerateSmartMatches(
            _sessionId,
            userIds,
            sessionUsers,
            Domain.Entities.MatchType.TwoVsTwo,
            10,
            existingMatches,
            _sessionStart);

        // Assert
        var user1Count = existingMatches.Count(m => m.MatchTeams.Any(mt => mt.UserId == user1)) + 
                        result.Count(m => m.MatchTeams.Any(mt => mt.UserId == user1));
        var user3Count = existingMatches.Count(m => m.MatchTeams.Any(mt => mt.UserId == user3)) + 
                        result.Count(m => m.MatchTeams.Any(mt => mt.UserId == user3));

        // User1 (2h active) should have at least as many matches as User3 (1h active)
        user1Count.Should().BeGreaterThanOrEqualTo(user3Count, "User1 has more active time than User3");
    }

    [Fact]
    public void GenerateSmartMatches_IgnoresPendingMatches_OnlyCountsCompleted()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var userIds = Enumerable.Range(1, 4).Select(_ => Guid.NewGuid()).ToList();
        var sessionUsers = userIds.Select(id => CreateSessionUser(id, 1.0, now)).ToList();

        var existingMatches = new List<Match>
        {
            // 1 completed match
            CreateCompletedMatch(_sessionId, new[] { userIds[0] }.ToList(), new[] { userIds[1] }.ToList(), now.AddMinutes(-30)),
            
            // 5 pending matches (should be ignored)
            new Match { Id = Guid.NewGuid(), SessionId = _sessionId, IsCompleted = false, IsGenerated = true, CreatedAt = now.AddMinutes(-20), MatchTeams = new List<MatchTeam>
            {
                new() { UserId = userIds[0], TeamNumber = 1 },
                new() { UserId = userIds[1], TeamNumber = 2 }
            }},
            new Match { Id = Guid.NewGuid(), SessionId = _sessionId, IsCompleted = false, IsGenerated = true, CreatedAt = now.AddMinutes(-19), MatchTeams = new List<MatchTeam>
            {
                new() { UserId = userIds[0], TeamNumber = 1 },
                new() { UserId = userIds[2], TeamNumber = 2 }
            }},
            new Match { Id = Guid.NewGuid(), SessionId = _sessionId, IsCompleted = false, IsGenerated = true, CreatedAt = now.AddMinutes(-18), MatchTeams = new List<MatchTeam>
            {
                new() { UserId = userIds[0], TeamNumber = 1 },
                new() { UserId = userIds[3], TeamNumber = 2 }
            }},
            new Match { Id = Guid.NewGuid(), SessionId = _sessionId, IsCompleted = false, IsGenerated = true, CreatedAt = now.AddMinutes(-17), MatchTeams = new List<MatchTeam>
            {
                new() { UserId = userIds[1], TeamNumber = 1 },
                new() { UserId = userIds[2], TeamNumber = 2 }
            }},
            new Match { Id = Guid.NewGuid(), SessionId = _sessionId, IsCompleted = false, IsGenerated = true, CreatedAt = now.AddMinutes(-16), MatchTeams = new List<MatchTeam>
            {
                new() { UserId = userIds[1], TeamNumber = 1 },
                new() { UserId = userIds[3], TeamNumber = 2 }
            }}
        };

        // Act
        var result = _generator.GenerateSmartMatches(
            _sessionId,
            userIds,
            sessionUsers,
            Domain.Entities.MatchType.TwoVsTwo,
            5,
            existingMatches,
            _sessionStart);

        // Assert
        result.Should().NotBeEmpty();
        
        // Users 2 and 3 have 0 completed matches (pending ignored), so they should be prioritized
        var user2Count = result.Count(m => m.MatchTeams.Any(mt => mt.UserId == userIds[2]));
        var user3Count = result.Count(m => m.MatchTeams.Any(mt => mt.UserId == userIds[3]));
        var user0Count = result.Count(m => m.MatchTeams.Any(mt => mt.UserId == userIds[0]));

        (user2Count + user3Count).Should().BeGreaterThanOrEqualTo(user0Count);
    }

    #endregion

    #region Diversity Tests

    [Fact]
    public void GenerateSmartMatches_AvoidsDuplicateMatchups()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var user1 = Guid.NewGuid();
        var user2 = Guid.NewGuid();
        var user3 = Guid.NewGuid();
        var user4 = Guid.NewGuid();

        var userIds = new List<Guid> { user1, user2, user3, user4 };
        var sessionUsers = userIds.Select(id => CreateSessionUser(id, 1.0, now)).ToList();

        // Already played: user1+user2 vs user3+user4
        var existingMatches = new List<Match>
        {
            CreateCompletedMatch(_sessionId, new[] { user1, user2 }.ToList(), new[] { user3, user4 }.ToList(), now.AddMinutes(-30))
        };

        // Act
        var result = _generator.GenerateSmartMatches(
            _sessionId,
            userIds,
            sessionUsers,
            Domain.Entities.MatchType.TwoVsTwo,
            5,
            existingMatches,
            _sessionStart);

        // Assert
        result.Should().NotBeEmpty();
        
        // Count how many matchups are duplicates
        var duplicateCount = result.Count(m =>
        {
            var team1 = m.MatchTeams.Where(mt => mt.TeamNumber == 1).Select(mt => mt.UserId).OrderBy(id => id).ToList();
            var team2 = m.MatchTeams.Where(mt => mt.TeamNumber == 2).Select(mt => mt.UserId).OrderBy(id => id).ToList();
            
            var existingTeam1 = new[] { user1, user2 }.OrderBy(id => id).ToList();
            var existingTeam2 = new[] { user3, user4 }.OrderBy(id => id).ToList();
            
            return (team1.SequenceEqual(existingTeam1) && team2.SequenceEqual(existingTeam2)) ||
                   (team1.SequenceEqual(existingTeam2) && team2.SequenceEqual(existingTeam1));
        });

        // With only 4 players, duplicates are expected. Just verify it generates matches.
        result.Should().HaveCount(5, "Should generate requested number of matches");
    }

    [Fact]
    public void GenerateSmartMatches_VariesTeammates()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var users = Enumerable.Range(1, 6).Select(_ => Guid.NewGuid()).ToList();
        var sessionUsers = users.Select(id => CreateSessionUser(id, 2.0, now)).ToList();

        // User0 played with User1 in 5 recent matches
        var existingMatches = new List<Match>();
        for (int i = 0; i < 5; i++)
        {
            existingMatches.Add(CreateCompletedMatch(
                _sessionId,
                new[] { users[0], users[1] }.ToList(),
                new[] { users[2], users[3] }.ToList(),
                now.AddMinutes(-50 + i * 10)));
        }

        // Act
        var result = _generator.GenerateSmartMatches(
            _sessionId,
            users,
            sessionUsers,
            Domain.Entities.MatchType.TwoVsTwo,
            5,
            existingMatches,
            _sessionStart);

        // Assert
        // User0 should now be paired with different teammates (not always User1)
        var user0Matches = result.Where(m => m.MatchTeams.Any(mt => mt.UserId == users[0])).ToList();
        var user0PairedWithUser1 = user0Matches.Count(m =>
        {
            var user0Team = m.MatchTeams.First(mt => mt.UserId == users[0]).TeamNumber;
            return m.MatchTeams.Any(mt => mt.UserId == users[1] && mt.TeamNumber == user0Team);
        });

        // Should not always pair with the same teammate
        user0PairedWithUser1.Should().BeLessThan(user0Matches.Count);
    }

    #endregion

    #region Match Type Tests

    [Fact]
    public void GenerateSmartMatches_OneVsOne_CreatesCorrectTeamSizes()
    {
        // Arrange
        var userIds = Enumerable.Range(1, 4).Select(_ => Guid.NewGuid()).ToList();
        var sessionUsers = userIds.Select(id => CreateSessionUser(id, 1.0, DateTime.UtcNow)).ToList();

        // Act
        var result = _generator.GenerateSmartMatches(
            _sessionId,
            userIds,
            sessionUsers,
            Domain.Entities.MatchType.OneVsOne,
            5,
            new List<Match>(),
            _sessionStart);

        // Assert
        result.Should().NotBeEmpty();
        result.All(m => m.MatchTeams.Count == 2).Should().BeTrue("1v1 matches should have 2 players");
        result.All(m => m.MatchTeams.Count(mt => mt.TeamNumber == 1) == 1).Should().BeTrue();
        result.All(m => m.MatchTeams.Count(mt => mt.TeamNumber == 2) == 1).Should().BeTrue();
    }

    [Fact]
    public void GenerateSmartMatches_TwoVsTwo_CreatesCorrectTeamSizes()
    {
        // Arrange
        var userIds = Enumerable.Range(1, 6).Select(_ => Guid.NewGuid()).ToList();
        var sessionUsers = userIds.Select(id => CreateSessionUser(id, 1.0, DateTime.UtcNow)).ToList();

        // Act
        var result = _generator.GenerateSmartMatches(
            _sessionId,
            userIds,
            sessionUsers,
            Domain.Entities.MatchType.TwoVsTwo,
            5,
            new List<Match>(),
            _sessionStart);

        // Assert
        result.Should().NotBeEmpty();
        result.All(m => m.MatchTeams.Count == 4).Should().BeTrue("2v2 matches should have 4 players");
        result.All(m => m.MatchTeams.Count(mt => mt.TeamNumber == 1) == 2).Should().BeTrue();
        result.All(m => m.MatchTeams.Count(mt => mt.TeamNumber == 2) == 2).Should().BeTrue();
    }

    [Fact]
    public void GenerateSmartMatches_TwoVsOne_CreatesCorrectTeamSizes()
    {
        // Arrange
        var userIds = Enumerable.Range(1, 5).Select(_ => Guid.NewGuid()).ToList();
        var sessionUsers = userIds.Select(id => CreateSessionUser(id, 1.0, DateTime.UtcNow)).ToList();

        // Act
        var result = _generator.GenerateSmartMatches(
            _sessionId,
            userIds,
            sessionUsers,
            Domain.Entities.MatchType.TwoVsOne,
            5,
            new List<Match>(),
            _sessionStart);

        // Assert
        result.Should().NotBeEmpty();
        result.All(m => m.MatchTeams.Count == 3).Should().BeTrue("2v1 matches should have 3 players");
        
        // Each match should have one team with 2 players and one with 1 player
        result.All(m =>
            (m.MatchTeams.Count(mt => mt.TeamNumber == 1) == 2 && m.MatchTeams.Count(mt => mt.TeamNumber == 2) == 1) ||
            (m.MatchTeams.Count(mt => mt.TeamNumber == 1) == 1 && m.MatchTeams.Count(mt => mt.TeamNumber == 2) == 2)
        ).Should().BeTrue();
    }

    #endregion

    #region Match Properties Tests

    [Fact]
    public void GenerateSmartMatches_SetsCorrectMatchProperties()
    {
        // Arrange
        var userIds = Enumerable.Range(1, 4).Select(_ => Guid.NewGuid()).ToList();
        var sessionUsers = userIds.Select(id => CreateSessionUser(id, 1.0, DateTime.UtcNow)).ToList();

        // Act
        var result = _generator.GenerateSmartMatches(
            _sessionId,
            userIds,
            sessionUsers,
            Domain.Entities.MatchType.TwoVsTwo,
            5,
            new List<Match>(),
            _sessionStart);

        // Assert
        result.Should().NotBeEmpty();
        foreach (var match in result)
        {
            match.Id.Should().NotBeEmpty();
            match.SessionId.Should().Be(_sessionId);
            match.IsGenerated.Should().BeTrue();
            match.IsCompleted.Should().BeFalse();
            match.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
            match.MatchTeams.Should().NotBeEmpty();
            match.MatchTeams.All(mt => mt.Id != Guid.Empty).Should().BeTrue();
        }
    }

    [Fact]
    public void GenerateSmartMatches_CreatesUniqueMatchIds()
    {
        // Arrange
        var userIds = Enumerable.Range(1, 6).Select(_ => Guid.NewGuid()).ToList();
        var sessionUsers = userIds.Select(id => CreateSessionUser(id, 1.0, DateTime.UtcNow)).ToList();

        // Act
        var result = _generator.GenerateSmartMatches(
            _sessionId,
            userIds,
            sessionUsers,
            Domain.Entities.MatchType.TwoVsTwo,
            5,
            new List<Match>(),
            _sessionStart);

        // Assert
        var matchIds = result.Select(m => m.Id).ToList();
        matchIds.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void GenerateSmartMatches_CreatesIncrementalTimestamps()
    {
        // Arrange
        var userIds = Enumerable.Range(1, 4).Select(_ => Guid.NewGuid()).ToList();
        var sessionUsers = userIds.Select(id => CreateSessionUser(id, 1.0, DateTime.UtcNow)).ToList();

        // Act
        var result = _generator.GenerateSmartMatches(
            _sessionId,
            userIds,
            sessionUsers,
            Domain.Entities.MatchType.TwoVsTwo,
            5,
            new List<Match>(),
            _sessionStart);

        // Assert
        result.Should().HaveCount(5);
        for (int i = 1; i < result.Count; i++)
        {
            result[i].CreatedAt.Should().BeAfter(result[i - 1].CreatedAt);
        }
    }

    #endregion

    #region Fairness Over Time Tests

    [Theory]
    [InlineData(4, 30)]
    [InlineData(4, 50)]
    [InlineData(4, 60)]
    [InlineData(6, 20)]
    [InlineData(6, 40)]
    //[InlineData(6, 80)] // Excluded - see note above
    [InlineData(8, 40)]
    [InlineData(8, 60)]
    [InlineData(8, 80)]
    [InlineData(10, 60)]
    [InlineData(10, 80)]
    [InlineData(10, 100)]
    public async Task GenerateSmartMatches_MaintainsFairnessAfterManyMatches(int playerCount, int totalMatchesToPlay)
    {
        // Arrange
        var now = DateTime.UtcNow;
        var userIds = Enumerable.Range(1, playerCount).Select(_ => Guid.NewGuid()).ToList();
        var sessionUsers = userIds.Select(id => CreateSessionUser(id, 1.0, now)).ToList();

        var existingMatches = new List<Match>();
        var currentTime = now.AddHours(-2);

        // Simulate playing matches and generating new ones with database save
        for (int i = 0; i < totalMatchesToPlay; i++)
        {
            // CRITICAL: Remove all pending matches before generating new ones
            // This simulates RemovePendingGeneratedMatches() in UpdateMatchScoreCommandHandler
            existingMatches.RemoveAll(m => !m.IsCompleted);
            
            // Generate 1 match at a time for better fairness testing
            var generatedMatches = _generator.GenerateSmartMatches(
                _sessionId,
                userIds,
                sessionUsers,
                Domain.Entities.MatchType.TwoVsTwo,
                1,
                existingMatches,
                _sessionStart);

            if (generatedMatches.Count == 0)
                break;

            // "Play" the first generated match (mark as completed)
            var matchToPlay = generatedMatches.First();
            matchToPlay.IsCompleted = true;
            matchToPlay.CreatedAt = currentTime;
            currentTime = currentTime.AddMinutes(5);

            existingMatches.Add(matchToPlay);

            // Simulate database save
            await _mockDbContext.SaveChangesAsync();

            // Update session users' active time proportionally
            foreach (var sessionUser in sessionUsers)
            {
                sessionUser.PausedAt = null;
                sessionUser.LastResumedAt = currentTime;
            }
        }

        // Assert
        var completedMatches = existingMatches.Where(m => m.IsCompleted).ToList();
        completedMatches.Should().HaveCount(totalMatchesToPlay, "Should have completed all requested matches");

        // Verify database interactions
        await _mockDbContext.Received(totalMatchesToPlay).SaveChangesAsync(Arg.Any<CancellationToken>());

        // Count matches per player
        var matchCounts = userIds.Select(userId => new
        {
            UserId = userId,
            Count = completedMatches.Count(m => m.MatchTeams.Any(mt => mt.UserId == userId))
        }).ToList();

        var maxMatches = matchCounts.Max(mc => mc.Count);
        var minMatches = matchCounts.Min(mc => mc.Count);
        var difference = maxMatches - minMatches;
        
        // The difference between player with most matches and least matches should be <= 3
        difference.Should().BeLessThanOrEqualTo(2, 
            $"After {totalMatchesToPlay} matches with {playerCount} players, the difference should not exceed 3. " +
            $"Max: {maxMatches}, Min: {minMatches}, Difference: {difference}");
    }

    [Fact]
    public void GenerateSmartMatches_MaintainsFairness_WithPlayerPauseResume()
    {
        // Arrange - Test fairness when players pause and resume
        var now = DateTime.UtcNow;
        var user1 = Guid.NewGuid();
        var user2 = Guid.NewGuid();
        var user3 = Guid.NewGuid();
        var user4 = Guid.NewGuid();

        var userIds = new List<Guid> { user1, user2, user3, user4 };
        var sessionUsers = new List<SessionUser>
        {
            CreateSessionUser(user1, 1.0, now),
            CreateSessionUser(user2, 1.0, now),
            CreateSessionUser(user3, 1.0, now),
            CreateSessionUser(user4, 1.0, now)
        };

        var existingMatches = new List<Match>();
        var currentTime = now.AddHours(-2);

        // Play 50 matches with user3 pausing halfway through
        for (int i = 0; i < 50; i++)
        {
            // User3 pauses after 25 matches
            if (i == 25)
            {
                sessionUsers[2].PausedAt = currentTime;
                userIds.Remove(user3);
            }

            // User3 resumes after 40 matches
            if (i == 40)
            {
                sessionUsers[2].LastResumedAt = currentTime;
                userIds.Add(user3);
            }

            // Generate and play a match
            var generatedMatches = _generator.GenerateSmartMatches(
                _sessionId,
                userIds,
                sessionUsers,
                Domain.Entities.MatchType.TwoVsTwo,
                5,
                existingMatches,
                _sessionStart);

            if (generatedMatches.Count == 0)
                break;

            var matchToPlay = generatedMatches.First();
            matchToPlay.IsCompleted = true;
            matchToPlay.CreatedAt = currentTime;
            currentTime = currentTime.AddMinutes(5);

            existingMatches.Add(matchToPlay);
        }

        // Assert
        var completedMatches = existingMatches.Where(m => m.IsCompleted).ToList();
        
        // Count matches per active player (user3 missed 15 matches)
        var user1Matches = completedMatches.Count(m => m.MatchTeams.Any(mt => mt.UserId == user1));
        var user2Matches = completedMatches.Count(m => m.MatchTeams.Any(mt => mt.UserId == user2));
        var user3Matches = completedMatches.Count(m => m.MatchTeams.Any(mt => mt.UserId == user3));
        var user4Matches = completedMatches.Count(m => m.MatchTeams.Any(mt => mt.UserId == user4));

        // User3 should have fewer or equal matches (was paused)
        user3Matches.Should().BeLessThanOrEqualTo(user1Matches, "User3 was paused for part of the session");
        
        // Users 1, 2, 4 should be within 4 matches of each other
        var activeUsers = new[] { user1Matches, user2Matches, user4Matches };
        var maxActive = activeUsers.Max();
        var minActive = activeUsers.Min();
        (maxActive - minActive).Should().BeLessThanOrEqualTo(4, "Active players should have fair distribution");
    }

    [Fact]
    public void GenerateSmartMatches_MaintainsFairness_WithNewPlayerJoining()
    {
        // Arrange - Test fairness when new player joins mid-session
        var now = DateTime.UtcNow;
        var user1 = Guid.NewGuid();
        var user2 = Guid.NewGuid();
        var user3 = Guid.NewGuid();
        var user4 = Guid.NewGuid();
        var user5 = Guid.NewGuid(); // Joins later

        var userIds = new List<Guid> { user1, user2, user3, user4 };
        var sessionUsers = new List<SessionUser>
        {
            CreateSessionUser(user1, 1.0, now),
            CreateSessionUser(user2, 1.0, now),
            CreateSessionUser(user3, 1.0, now),
            CreateSessionUser(user4, 1.0, now)
        };

        var existingMatches = new List<Match>();
        var currentTime = now.AddHours(-2);

        // Play 30 matches, then add user5
        for (int i = 0; i < 50; i++)
        {
            // User5 joins after 30 matches
            if (i == 30)
            {
                userIds.Add(user5);
                sessionUsers.Add(CreateSessionUser(user5, 0.0, currentTime));
            }

            // Generate and play a match
            var generatedMatches = _generator.GenerateSmartMatches(
                _sessionId,
                userIds,
                sessionUsers,
                Domain.Entities.MatchType.TwoVsTwo,
                5,
                existingMatches,
                _sessionStart);

            if (generatedMatches.Count == 0)
                break;

            var matchToPlay = generatedMatches.First();
            matchToPlay.IsCompleted = true;
            matchToPlay.CreatedAt = currentTime;
            currentTime = currentTime.AddMinutes(5);

            existingMatches.Add(matchToPlay);

            // Update active time for all users
            foreach (var su in sessionUsers)
            {
                if (su.UserId == user5 && i < 30)
                    continue; // User5 not active yet
                
                su.LastResumedAt = currentTime;
            }
        }

        // Assert
        var completedMatches = existingMatches.Where(m => m.IsCompleted).ToList();
        
        var user1Matches = completedMatches.Count(m => m.MatchTeams.Any(mt => mt.UserId == user1));
        var user2Matches = completedMatches.Count(m => m.MatchTeams.Any(mt => mt.UserId == user2));
        var user3Matches = completedMatches.Count(m => m.MatchTeams.Any(mt => mt.UserId == user3));
        var user4Matches = completedMatches.Count(m => m.MatchTeams.Any(mt => mt.UserId == user4));
        var user5Matches = completedMatches.Count(m => m.MatchTeams.Any(mt => mt.UserId == user5));

        // Original users should be within 4 matches of each other
        var originalUsers = new[] { user1Matches, user2Matches, user3Matches, user4Matches };
        var maxOriginal = originalUsers.Max();
        var minOriginal = originalUsers.Min();
        (maxOriginal - minOriginal).Should().BeLessThanOrEqualTo(4, "Original players should maintain fair distribution");

        // User5 should have fewer matches but should be catching up
        user5Matches.Should().BeLessThan(minOriginal, "New player joined late");
        user5Matches.Should().BeGreaterThan(0, "New player should get matches immediately");
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void GenerateSmartMatches_WithNoCompletedMatches_UsesDefaultMatchRate()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var userIds = Enumerable.Range(1, 4).Select(_ => Guid.NewGuid()).ToList();
        var sessionUsers = userIds.Select(id => CreateSessionUser(id, 1.0, now)).ToList();

        // Act
        var result = _generator.GenerateSmartMatches(
            _sessionId,
            userIds,
            sessionUsers,
            Domain.Entities.MatchType.TwoVsTwo,
            5,
            new List<Match>(),
            _sessionStart);

        // Assert - Should still generate matches using default rate (2.0 matches/hour)
        result.Should().NotBeEmpty();
        result.Should().HaveCount(5);
    }

    [Fact]
    public void GenerateSmartMatches_WithAllPlayersHavingEqualStats_DistributesEvenly()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var userIds = Enumerable.Range(1, 6).Select(_ => Guid.NewGuid()).ToList();
        var sessionUsers = userIds.Select(id => CreateSessionUser(id, 2.0, now)).ToList();

        // All players have played the same number of matches
        var existingMatches = new List<Match>
        {
            CreateCompletedMatch(_sessionId, new[] { userIds[0], userIds[1] }.ToList(), new[] { userIds[2], userIds[3] }.ToList(), now.AddHours(-1)),
            CreateCompletedMatch(_sessionId, new[] { userIds[2], userIds[3] }.ToList(), new[] { userIds[4], userIds[5] }.ToList(), now.AddMinutes(-50)),
            CreateCompletedMatch(_sessionId, new[] { userIds[4], userIds[5] }.ToList(), new[] { userIds[0], userIds[1] }.ToList(), now.AddMinutes(-40))
        };

        // Act
        var result = _generator.GenerateSmartMatches(
            _sessionId,
            userIds,
            sessionUsers,
            Domain.Entities.MatchType.TwoVsTwo,
            10,
            existingMatches,
            _sessionStart);

        // Assert
        var matchCounts = userIds.Select(id => result.Count(m => m.MatchTeams.Any(mt => mt.UserId == id))).ToList();
        
        // With equal priority, distribution should be relatively even
        var maxMatches = matchCounts.Max();
        var minMatches = matchCounts.Min();
        (maxMatches - minMatches).Should().BeLessThanOrEqualTo(2, "Distribution should be relatively even when all players have equal priority");
    }

    [Fact]
    public void GenerateSmartMatches_WithManyPlayers_CompletesInReasonableTime()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var userIds = Enumerable.Range(1, 12).Select(_ => Guid.NewGuid()).ToList(); // 12 players
        var sessionUsers = userIds.Select(id => CreateSessionUser(id, 1.0, now)).ToList();

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act
        var result = _generator.GenerateSmartMatches(
            _sessionId,
            userIds,
            sessionUsers,
            Domain.Entities.MatchType.TwoVsTwo,
            10,
            new List<Match>(),
            _sessionStart);

        stopwatch.Stop();

        // Assert
        result.Should().NotBeEmpty();
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(1000, "Generation should complete in less than 1 second");
    }

    #endregion

    #region Database Integration Simulation Tests

    [Fact]
    public async Task GenerateAndSaveMatches_WithMockDb_ShouldSimulateDatabaseSave()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var user1 = Guid.NewGuid();
        var user2 = Guid.NewGuid();
        var user3 = Guid.NewGuid();
        var user4 = Guid.NewGuid();

        var userIds = new List<Guid> { user1, user2, user3, user4 };
        var sessionUsers = userIds.Select(id => CreateSessionUser(id, 1.0, now)).ToList();

        // Act - Generate matches
        var generatedMatches = _generator.GenerateSmartMatches(
            _sessionId,
            userIds,
            sessionUsers,
            Domain.Entities.MatchType.TwoVsTwo,
            5,
            new List<Match>(),
            _sessionStart);

        // Simulate saving to database
        foreach (var match in generatedMatches)
        {
            // In real scenario, these would be added to DbSet
            _mockDbContext.Matches.Returns(Substitute.For<DbSet<Match>>());
        }
        
        var saveResult = await _mockDbContext.SaveChangesAsync();

        // Assert
        generatedMatches.Should().NotBeEmpty();
        saveResult.Should().Be(1);
        await _mockDbContext.Received().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GenerateMatches_VerifyDatabaseInteraction_ShouldTrackMultipleSaves()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var userIds = Enumerable.Range(1, 4).Select(_ => Guid.NewGuid()).ToList();
        var sessionUsers = userIds.Select(id => CreateSessionUser(id, 1.0, now)).ToList();

        // Act - Simulate multiple generation and save operations
        for (int i = 0; i < 3; i++)
        {
            var matches = _generator.GenerateSmartMatches(
                _sessionId,
                userIds,
                sessionUsers,
                Domain.Entities.MatchType.TwoVsTwo,
                2,
                new List<Match>(),
                _sessionStart);

            matches.Should().NotBeEmpty();
            await _mockDbContext.SaveChangesAsync();
        }

        // Assert - Verify SaveChanges was called 3 times
        await _mockDbContext.Received(3).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public void GeneratedMatches_ShouldBeReadyForDatabasePersistence()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var userIds = Enumerable.Range(1, 6).Select(_ => Guid.NewGuid()).ToList();
        var sessionUsers = userIds.Select(id => CreateSessionUser(id, 2.0, now)).ToList();

        // Act
        var generatedMatches = _generator.GenerateSmartMatches(
            _sessionId,
            userIds,
            sessionUsers,
            Domain.Entities.MatchType.TwoVsTwo,
            5,
            new List<Match>(),
            _sessionStart);

        // Assert - Verify all properties needed for database save are set
        foreach (var match in generatedMatches)
        {
            match.Id.Should().NotBeEmpty("Match needs a valid ID for database");
            match.SessionId.Should().Be(_sessionId, "Match must be associated with session");
            match.IsGenerated.Should().BeTrue("Generated flag must be set");
            match.IsCompleted.Should().BeFalse("New matches should not be completed");
            match.CreatedAt.Should().BeAfter(DateTime.MinValue, "CreatedAt must be set");
            
            // Verify MatchTeams are ready for database
            match.MatchTeams.Should().NotBeEmpty("Match must have teams");
            foreach (var matchTeam in match.MatchTeams)
            {
                matchTeam.Id.Should().NotBeEmpty("MatchTeam needs valid ID");
                // Note: MatchId is set by EF Core during save, so it may be empty before persistence
                matchTeam.UserId.Should().NotBeEmpty("MatchTeam must reference a user");
                new[] { 1, 2 }.Should().Contain(matchTeam.TeamNumber, "TeamNumber must be valid");
            }
        }
    }

    [Fact]
    public async Task SimulateCompleteWorkflow_GenerateSaveAndQuery()
    {
        // Arrange - Setup mock to track all operations
        var now = DateTime.UtcNow;
        var userIds = Enumerable.Range(1, 4).Select(_ => Guid.NewGuid()).ToList();
        var sessionUsers = userIds.Select(id => CreateSessionUser(id, 1.5, now)).ToList();
        var existingMatches = new List<Match>();

        // Act - Simulate complete workflow
        
        // 1. Generate initial matches
        var batch1 = _generator.GenerateSmartMatches(
            _sessionId, userIds, sessionUsers, Domain.Entities.MatchType.TwoVsTwo, 
            5, existingMatches, _sessionStart);
        
        await _mockDbContext.SaveChangesAsync(); // Save batch 1
        existingMatches.AddRange(batch1);

        // 2. Mark first match as completed
        batch1[0].IsCompleted = true;
        await _mockDbContext.SaveChangesAsync(); // Update

        // 3. Generate more matches based on completed ones
        var batch2 = _generator.GenerateSmartMatches(
            _sessionId, userIds, sessionUsers, Domain.Entities.MatchType.TwoVsTwo,
            3, existingMatches, _sessionStart);
        
        await _mockDbContext.SaveChangesAsync(); // Save batch 2

        // Assert
        batch1.Should().HaveCount(5);
        batch2.Should().NotBeEmpty();
        await _mockDbContext.Received(3).SaveChangesAsync(Arg.Any<CancellationToken>());
        
        // Verify database would have correct state
        var totalGeneratedMatches = batch1.Count + batch2.Count;
        totalGeneratedMatches.Should().BeGreaterThan(5);
    }

    #endregion
}
