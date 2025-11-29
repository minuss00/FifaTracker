namespace FifaTracker.Application.Services;

/// <summary>
/// Simple cache interface for storing and retrieving match combinations
/// </summary>
public interface IMatchCombinationCache
{
    /// <summary>
    /// Check if cache has combinations for the given session
    /// </summary>
    bool HasCombinationsForSession(Guid sessionId);
    
    /// <summary>
    /// Get all combinations for the given session
    /// </summary>
    List<CachedMatchCombination> GetCombinationsForSession(Guid sessionId);
    
    /// <summary>
    /// Store combinations for the given session (replaces existing)
    /// </summary>
    void StoreCombinationsForSession(Guid sessionId, List<CachedMatchCombination> combinations);
    
    /// <summary>
    /// Get a specific combination by team composition
    /// </summary>
    CachedMatchCombination? GetCombination(Guid sessionId, List<Guid> team1UserIds, List<Guid> team2UserIds);
    
    /// <summary>
    /// Update times played for a specific combination
    /// </summary>
    void UpdateTimesPlayed(Guid sessionId, List<Guid> team1UserIds, List<Guid> team2UserIds, int timesPlayed);
    
    /// <summary>
    /// Increment times played for a specific combination (when match is completed)
    /// </summary>
    void IncrementTimesPlayed(Guid sessionId, List<Guid> team1UserIds, List<Guid> team2UserIds);
}
