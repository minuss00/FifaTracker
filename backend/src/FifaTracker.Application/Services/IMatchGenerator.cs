using FifaTracker.Domain.Entities;

namespace FifaTracker.Application.Services;

public interface IMatchGenerator
{
    List<Match> GenerateSmartMatches(
        Guid sessionId, 
        List<Guid> userIds,
        List<SessionUser> sessionUsers,
        FifaTracker.Domain.Entities.MatchType matchType,
        int targetCount,
        List<Match> existingMatches,
        DateTime sessionStartTime);
}
