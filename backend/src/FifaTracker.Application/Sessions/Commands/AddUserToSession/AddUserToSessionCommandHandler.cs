using FifaTracker.Domain.Entities;
using FifaTracker.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FifaTracker.Application.Sessions.Commands.AddUserToSession;

public class AddUserToSessionCommandHandler : IRequestHandler<AddUserToSessionCommand, Unit>
{
    private readonly IApplicationDbContext _context;

    public AddUserToSessionCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Unit> Handle(AddUserToSessionCommand request, CancellationToken cancellationToken)
    {
        var session = await _context.Sessions
            .Include(s => s.SessionUsers)
            .FirstOrDefaultAsync(s => s.Id == request.SessionId, cancellationToken);

        if (session == null)
            throw new KeyNotFoundException($"Session with ID {request.SessionId} not found");

        // Check if user exists and is active
        var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken);
        if (user == null)
            throw new KeyNotFoundException($"User with ID {request.UserId} not found");
        if (!user.IsActive)
            throw new InvalidOperationException("Cannot add inactive user to session");

        // Check if user is already in session
        if (session.SessionUsers.Any(su => su.UserId == request.UserId))
            throw new InvalidOperationException("User is already in this session");

        // Add user to session
        var sessionUser = new SessionUser
        {
            SessionId = request.SessionId,
            UserId = request.UserId,
            JoinedAt = DateTime.UtcNow
        };
        _context.SessionUsers.Add(sessionUser);

        // Remove all pending generated matches (keep completed and custom matches)
        var pendingGeneratedMatches = await _context.Matches
            .Where(m => m.SessionId == request.SessionId && !m.IsCompleted && m.IsGenerated)
            .ToListAsync(cancellationToken);

        foreach (var match in pendingGeneratedMatches)
        {
            _context.Matches.Remove(match);
        }
        
        session.LastModifiedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
