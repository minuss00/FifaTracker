using MediatR;

namespace FifaTracker.Application.Matches.Commands.CompleteMatch;

public record CompleteMatchCommand(
    Guid SessionId,
    List<Guid> Team1UserIds,
    List<Guid> Team2UserIds,
    int Team1Score,
    int Team2Score
) : IRequest<Guid>;
