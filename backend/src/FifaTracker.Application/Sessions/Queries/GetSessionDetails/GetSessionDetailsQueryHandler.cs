using FifaTracker.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FifaTracker.Application.Sessions.Queries.GetSessionDetails;

public class GetSessionDetailsQueryHandler : IRequestHandler<GetSessionDetailsQuery, SessionDetailsDto>
{
    private readonly IApplicationDbContext _context;

    public GetSessionDetailsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<SessionDetailsDto> Handle(GetSessionDetailsQuery request, CancellationToken cancellationToken)
    {
        var session = await _context.Sessions
            .AsNoTracking()
            .Include(s => s.SessionUsers)
                .ThenInclude(su => su.User)
            .Include(s => s.Matches)
                .ThenInclude(m => m.MatchTeams)
                    .ThenInclude(mt => mt.User)
            .FirstOrDefaultAsync(s => s.Id == request.SessionId, cancellationToken);

        if (session == null)
            throw new KeyNotFoundException($"Session with ID {request.SessionId} not found");

        var users = session.SessionUsers.Select(su => new SessionUserDto(
            su.UserId,
            su.User.Name,
            su.JoinedAt,
            su.IsActiveInSession,
            su.PausedAt,
            su.TotalActiveTime
        )).ToList();

        var matches = session.Matches.Select(m => new MatchDto(
            m.Id,
            m.IsGenerated,
            m.IsCompleted,
            m.Team1Score,
            m.Team2Score,
            m.PlayedAt,
            m.MatchTeams.Where(mt => mt.TeamNumber == 1).Select(mt => new MatchPlayerDto(mt.UserId, mt.User.Name)).ToList(),
            m.MatchTeams.Where(mt => mt.TeamNumber == 2).Select(mt => new MatchPlayerDto(mt.UserId, mt.User.Name)).ToList()
        )).ToList();

        return new SessionDetailsDto(
            session.Id,
            session.Name,
            session.StartDate,
            session.EndDate,
            session.Status,
            session.MatchType,
            users,
            matches
        );
    }
}
