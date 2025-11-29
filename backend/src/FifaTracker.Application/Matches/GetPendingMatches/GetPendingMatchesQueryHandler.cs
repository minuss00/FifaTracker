using FifaTracker.Application.Services;
using FifaTracker.Application.Sessions.Queries.GetSessionDetails;
using FifaTracker.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FifaTracker.Application.Matches.GetPendingMatches;

public class GetPendingMatchesQueryHandler : IRequestHandler<GetPendingMatchesQuery, List<MatchDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly IMatchGenerator _matchGenerator;

    public GetPendingMatchesQueryHandler(IApplicationDbContext context, IMatchGenerator matchGenerator)
    {
        _context = context;
        _matchGenerator = matchGenerator;
    }

    public async Task<List<MatchDto>> Handle(GetPendingMatchesQuery request, CancellationToken cancellationToken)
    {
        // Get session with users
        var session = await _context.Sessions
            .Include(s => s.SessionUsers)
                .ThenInclude(su => su.User)
            .FirstOrDefaultAsync(s => s.Id == request.SessionId, cancellationToken);

        if (session == null)
            throw new InvalidOperationException($"Session {request.SessionId} not found");

        // Get all pending matches (generated + custom) sorted by priority
        return await _matchGenerator.GetPendingMatchesAsync(session, cancellationToken);
    }
}
