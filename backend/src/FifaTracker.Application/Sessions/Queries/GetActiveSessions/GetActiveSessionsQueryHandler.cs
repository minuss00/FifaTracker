using FifaTracker.Domain.Entities;
using FifaTracker.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FifaTracker.Application.Sessions.Queries.GetActiveSessions;

public class GetActiveSessionsQueryHandler : IRequestHandler<GetActiveSessionsQuery, List<SessionSummaryDto>>
{
    private readonly IApplicationDbContext _context;

    public GetActiveSessionsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<SessionSummaryDto>> Handle(GetActiveSessionsQuery request, CancellationToken cancellationToken)
    {
        var sessions = await _context.Sessions
            .AsNoTracking()
            .Where(s => s.Status == SessionStatus.Active)
            .Include(s => s.Matches)
            .Include(s => s.SessionUsers)
            .OrderByDescending(s => s.StartDate)
            .ToListAsync(cancellationToken);

        return sessions.Select(s => new SessionSummaryDto(
            s.Id,
            s.Name,
            s.StartDate,
            s.Status.ToString(),
            s.MatchType.ToString(),
            s.Matches.Count,
            s.Matches.Count(m => m.IsCompleted),
            s.SessionUsers.Count
        )).ToList();
    }
}
