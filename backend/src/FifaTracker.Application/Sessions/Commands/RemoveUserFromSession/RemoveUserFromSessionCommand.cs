using MediatR;

namespace FifaTracker.Application.Sessions.Commands.RemoveUserFromSession;

public record RemoveUserFromSessionCommand(Guid SessionId, Guid UserId) : IRequest<Unit>;
