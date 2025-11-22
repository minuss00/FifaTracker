using MediatR;

namespace FifaTracker.Application.Sessions.Commands.ResumeUserInSession;

public record ResumeUserInSessionCommand(Guid SessionId, Guid UserId) : IRequest<Unit>;
