using FifaTracker.Application.Sessions.Queries.GetSessionDetails;
using MediatR;

namespace FifaTracker.Application.Matches.GetPendingMatches;

public record GetPendingMatchesQuery(Guid SessionId) : IRequest<List<MatchDto>>;
