namespace FifaTracker.Application.Services;

/// <summary>
/// Represents a cached match combination with metadata
/// </summary>
public class CachedMatchCombination
{
    public Guid SessionId { get; set; }
    public List<Guid> Team1UserIds { get; set; } = new();
    public List<Guid> Team2UserIds { get; set; } = new();
    public int TimesPlayed { get; set; }
    public double Priority { get; set; } // Not stored in cache
}

/// <summary>
/// Simple cache for storing match combinations by session
/// </summary>
public class MatchCombinationCache : IMatchCombinationCache
{
    // Key format: "{sessionId}_{team1Sorted}_vs_{team2Sorted}"
    private readonly Dictionary<string, CachedMatchCombination> _cache = new();
    private readonly object _lock = new();

    public bool HasCombinationsForSession(Guid sessionId)
    {
        lock (_lock)
        {
            return _cache.Keys.Any(k => k.StartsWith($"{sessionId}_"));
        }
    }

    public List<CachedMatchCombination> GetCombinationsForSession(Guid sessionId)
    {
        lock (_lock)
        {
            return _cache
                .Where(kvp => kvp.Value.SessionId == sessionId)
                .Select(kvp => kvp.Value)
                .ToList();
        }
    }

    public void StoreCombinationsForSession(Guid sessionId, List<CachedMatchCombination> combinations)
    {
        lock (_lock)
        {
            // Remove old combinations for this session
            var keysToRemove = _cache.Keys.Where(k => k.StartsWith($"{sessionId}_")).ToList();
            foreach (var key in keysToRemove)
            {
                _cache.Remove(key);
            }

            // Add new combinations
            foreach (var combo in combinations)
            {
                var key = CreateCacheKey(sessionId, combo.Team1UserIds, combo.Team2UserIds);
                _cache[key] = combo;
            }
        }
    }

    public CachedMatchCombination? GetCombination(Guid sessionId, List<Guid> team1UserIds, List<Guid> team2UserIds)
    {
        lock (_lock)
        {
            var key = CreateCacheKey(sessionId, team1UserIds, team2UserIds);
            return _cache.TryGetValue(key, out var combo) ? combo : null;
        }
    }

    public void UpdateTimesPlayed(Guid sessionId, List<Guid> team1UserIds, List<Guid> team2UserIds, int timesPlayed)
    {
        lock (_lock)
        {
            var key = CreateCacheKey(sessionId, team1UserIds, team2UserIds);
            if (_cache.TryGetValue(key, out var combo))
            {
                combo.TimesPlayed = timesPlayed;
            }
        }
    }

    public void IncrementTimesPlayed(Guid sessionId, List<Guid> team1UserIds, List<Guid> team2UserIds)
    {
        lock (_lock)
        {
            var key = CreateCacheKey(sessionId, team1UserIds, team2UserIds);
            if (_cache.TryGetValue(key, out var combo))
            {
                combo.TimesPlayed++;
            }
        }
    }

    private static string CreateCacheKey(Guid sessionId, List<Guid> team1UserIds, List<Guid> team2UserIds)
    {
        var team1Sorted = string.Join(",", team1UserIds.OrderBy(id => id));
        var team2Sorted = string.Join(",", team2UserIds.OrderBy(id => id));
        
        // Order teams to ensure team1 vs team2 == team2 vs team1
        var teams = new[] { team1Sorted, team2Sorted }.OrderBy(t => t).ToArray();
        return $"{sessionId}_{teams[0]}_vs_{teams[1]}";
    }
}
