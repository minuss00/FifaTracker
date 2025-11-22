using FifaTracker.Domain.Entities;
using FifaTracker.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace FifaTracker.Application.Services;

public static class MatchManagementExtensions
{
    public static async Task<List<Match>> GetMatchesForSessionAsync(
        this IApplicationDbContext context,
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        return await context.Matches
            .Where(m => m.SessionId == sessionId)
            .Include(m => m.MatchTeams)
            .ToListAsync(cancellationToken);
    }

    public static List<Match> RemovePendingGeneratedMatches(
        this IApplicationDbContext context,
        List<Match> matches)
    {
        var pendingGenerated = matches.Where(m => !m.IsCompleted && m.IsGenerated).ToList();
        
        foreach (var match in pendingGenerated)
        {
            context.Matches.Remove(match);
            matches.Remove(match);
        }
        
        return matches;
    }

    public static List<Match> RemovePendingGeneratedMatchesWithUser(
        this IApplicationDbContext context,
        List<Match> matches,
        Guid userId)
    {
        var toRemove = matches
            .Where(m => !m.IsCompleted && m.IsGenerated && m.MatchTeams.Any(mt => mt.UserId == userId))
            .ToList();
        
        foreach (var match in toRemove)
        {
            context.Matches.Remove(match);
            matches.Remove(match);
        }
        
        return matches;
    }
}
