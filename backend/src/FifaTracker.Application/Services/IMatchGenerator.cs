using FifaTracker.Domain.Entities;

namespace FifaTracker.Application.Services;

public interface IMatchGenerator
{
    /// <summary>
    /// Filters out combinations that already exist as completed matches.
    /// Returns only pending combinations with calculated priority.
    /// Custom matches always have Double.MaxValue priority (appear first).
    /// </summary>
    List<Match> GetPendingMatches(
        Guid sessionId,
        List<Guid> userIds,
        List<SessionUser> sessionUsers,
        Domain.Entities.MatchType matchType,
        List<Match> completedMatches,
        DateTime sessionStartTime);

    /// <summary>
    /// Generates all possible match combinations for given players and match type.
    /// Does not save to database - returns in-memory list.
    /// </summary>
    List<Match> GenerateAllPossibleCombinations(
        Guid sessionId,
        List<Guid> userIds,
        Domain.Entities.MatchType matchType);

    /// <summary>
    /// Calculates priority for a pending match based on:
    /// - Fairness: players with fewer completed matches get higher priority
    /// - Avoiding repetition: penalty if players were in the last completed match
    /// - Time in session: bonus for players with more active time
    /// </summary>
    double CalculateMatchPriority(
        Match match,
        List<SessionUser> sessionUsers,
        List<Match> completedMatches,
        List<Match> allCombinations,
        DateTime sessionStartTime);
}
