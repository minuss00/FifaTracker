using FifaTracker.Domain.Entities;

namespace FifaTracker.Domain.Extensions;

public static class SessionUserExtensions
{
    /// <summary>
    /// Updates TotalActiveTime by adding current active period and resets LastResumedAt.
    /// Call this when pausing a user or during periodic snapshots.
    /// </summary>
    public static void SnapshotActiveTime(this SessionUser sessionUser, DateTime now)
    {
        if (!sessionUser.IsActiveInSession)
            return;
        
        var currentPeriodStart = sessionUser.LastResumedAt ?? sessionUser.JoinedAt;
        var currentPeriod = now - currentPeriodStart;
        sessionUser.TotalActiveTime += currentPeriod;
        sessionUser.LastResumedAt = now;
    }
    
    /// <summary>
    /// Gets total active time including current period if user is active.
    /// Use this for calculations without modifying database state.
    /// </summary>
    public static double GetCurrentActiveTotalHours(this SessionUser sessionUser, DateTime now)
    {
        if (!sessionUser.IsActiveInSession)
            return sessionUser.TotalActiveTime.TotalHours;
        
        var currentPeriodStart = sessionUser.LastResumedAt ?? sessionUser.JoinedAt;
        var currentPeriod = now - currentPeriodStart;
        return (sessionUser.TotalActiveTime + currentPeriod).TotalHours;
    }
}
