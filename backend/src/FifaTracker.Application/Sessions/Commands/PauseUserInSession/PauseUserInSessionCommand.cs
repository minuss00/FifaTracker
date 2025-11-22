using MediatR;

namespace FifaTracker.Application.Sessions.Commands.PauseUserInSession;

public record PauseUserInSessionCommand(Guid SessionId, Guid UserId) : IRequest<Unit>;
