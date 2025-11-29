using FifaTracker.Application.Sessions.Queries.GetSessionDetails;
using FifaTracker.Domain.Entities;

namespace FifaTracker.Application.Services;

public interface IMatchGenerator
{
    /// <summary>
    /// Gets all pending matches (both generated and custom) sorted by priority.
    /// Uses cache for all possible combinations and returns them as DTOs.
    /// Custom matches always appear first (Double.MaxValue priority).
    /// Includes TimesPlayed count for each match combination.
    /// </summary>
    /// <param name="session">Session</param>
    /// <returns>List of match DTOs sorted by priority (custom first, then by calculated priority)</returns>
    Task<List<MatchDto>> GetPendingMatchesAsync(Session session, CancellationToken cancellationToken);   
}
