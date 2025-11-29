using FifaTracker.Application.Users.Queries.GetAllUsers;
using FifaTracker.Domain.Entities;
using FifaTracker.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FifaTracker.Application.Users.Queries.GetUsersWithStats;

public class GetUsersWithStatsQueryHandler : IRequestHandler<GetUsersWithStatsQuery, List<UserDto>>
{
    private readonly IApplicationDbContext _context;

    public GetUsersWithStatsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<UserDto>> Handle(GetUsersWithStatsQuery request, CancellationToken cancellationToken)
    {
        var users = await _context.Users
            .AsNoTracking()
            .Where(u => u.IsActive)
            .Include(u => u.SessionUsers)
            .ThenInclude(su => su.Session)
            .OrderBy(u => u.Name)
            .ToListAsync(cancellationToken);

        // Get all completed sessions ordered by end date (newest first)
        var completedSessions = await _context.Sessions
            .AsNoTracking()
            .Where(s => s.Status == SessionStatus.Completed)
            .Include(s => s.SessionUsers)
            .OrderByDescending(s => s.EndDate ?? s.StartDate)
            .ToListAsync(cancellationToken);

        var result = new List<UserDto>();

        foreach (var user in users)
        {
            // Get user's sessions
            var userSessions = user.SessionUsers
                .Where(su => su.Session.Status == SessionStatus.Completed)
                .OrderByDescending(su => su.Session.EndDate ?? su.Session.StartDate)
                .ToList();

            // Last session date
            var lastSessionDate = userSessions.FirstOrDefault()?.Session.EndDate 
                ?? userSessions.FirstOrDefault()?.Session.StartDate;

            // Total sessions count
            var totalSessionsCount = userSessions.Count;

            // Total time spent (in minutes) with backward compatibility
            var totalMinutes = 0;
            foreach (var su in userSessions)
            {
                if (su.TotalActiveTime.TotalMinutes > 0)
                {
                    // Use tracked time if available
                    totalMinutes += (int)su.TotalActiveTime.TotalMinutes;
                }
                else
                {
                    // Backward compatibility: use session duration
                    var sessionStart = su.Session.StartDate;
                    var sessionEnd = su.Session.EndDate ?? DateTime.UtcNow;
                    totalMinutes += (int)(sessionEnd - sessionStart).TotalMinutes;
                }
            }
            // Card status calculation: count consecutive sessions without this user
            var cardStatus = CardStatus.None;
            if (userSessions.Count > 0)
            {
                cardStatus = CalculateCardStatus(user.Id, completedSessions);
            }

            result.Add(new UserDto(
                user.Id,
                user.Name,
                user.CreatedAt,
                lastSessionDate,
                totalSessionsCount,
                totalMinutes,
                cardStatus
            ));
        }

        return result;
    }

    private CardStatus CalculateCardStatus(Guid userId, List<Session> completedSessions)
    {
        // Take only the most recent sessions (up to 3)
        var recentSessions = completedSessions.Take(3).ToList();
        
        if (recentSessions.Count < 2)
        {
            return CardStatus.None;
        }

        // Count consecutive sessions without this user (from most recent)
        int missedCount = 0;
        foreach (var session in recentSessions)
        {
            var userInSession = session.SessionUsers.Any(su => su.UserId == userId);
            if (!userInSession)
            {
                missedCount++;
            }
            else
            {
                // Stop counting if user was in a session
                break;
            }
        }

        if (missedCount >= 3)
        {
            return CardStatus.Red;
        }
        else if (missedCount >= 2)
        {
            return CardStatus.Yellow;
        }

        return CardStatus.None;
    }
}
