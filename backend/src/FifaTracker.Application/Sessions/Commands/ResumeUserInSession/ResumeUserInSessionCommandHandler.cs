using FifaTracker.Application.Services;
using FifaTracker.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FifaTracker.Application.Sessions.Commands.ResumeUserInSession;

public class ResumeUserInSessionCommandHandler : IRequestHandler<ResumeUserInSessionCommand, Unit>
{
    private readonly IApplicationDbContext _context;
    private readonly IMatchGenerator _matchGenerator;

    public ResumeUserInSessionCommandHandler(IApplicationDbContext context, IMatchGenerator matchGenerator)
    {
        _context = context;
        _matchGenerator = matchGenerator;
    }

    public async Task<Unit> Handle(ResumeUserInSessionCommand request, CancellationToken cancellationToken)
    {
        var session = await _context.Sessions
            .Include(s => s.SessionUsers)
            .FirstOrDefaultAsync(s => s.Id == request.SessionId, cancellationToken);

        if (session == null)
            throw new KeyNotFoundException($"Session with ID {request.SessionId} not found");

        var sessionUser = session.SessionUsers.FirstOrDefault(su => su.UserId == request.UserId);
        if (sessionUser == null)
            throw new KeyNotFoundException($"User with ID {request.UserId} not found in session");

        if (sessionUser.IsActiveInSession)
            throw new InvalidOperationException("User is already active");

        // Resume user
        sessionUser.IsActiveInSession = true;
        sessionUser.LastResumedAt = DateTime.UtcNow;
        sessionUser.PausedAt = null;

        // Remove all pending generated matches
        var pendingGeneratedMatches = await _context.Matches
            .Where(m => m.SessionId == request.SessionId && !m.IsCompleted && m.IsGenerated)
            .ToListAsync(cancellationToken);

        foreach (var match in pendingGeneratedMatches)
        {
            _context.Matches.Remove(match);
        }

        // Get completed and custom matches
        var completedAndCustomMatches = await _context.Matches
            .Where(m => m.SessionId == request.SessionId && (m.IsCompleted || !m.IsGenerated))
            .Include(m => m.MatchTeams)
            .ToListAsync(cancellationToken);

        var activeUserIds = session.SessionUsers
            .Where(su => su.IsActiveInSession)
            .Select(su => su.UserId)
            .ToList();

        // Regenerate 5 matches including the resumed user
        if (activeUserIds.Count >= (session.MatchType == Domain.Entities.MatchType.OneVsOne ? 2 :
                                     session.MatchType == Domain.Entities.MatchType.TwoVsOne ? 3 : 4))
        {
            var newMatches = _matchGenerator.GenerateSmartMatches(
                session.Id,
                activeUserIds,
                session.SessionUsers.ToList(),
                session.MatchType,
                5,
                completedAndCustomMatches,
                session.StartDate);

            foreach (var match in newMatches)
            {
                _context.Matches.Add(match);
            }
        }

        session.LastModifiedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
