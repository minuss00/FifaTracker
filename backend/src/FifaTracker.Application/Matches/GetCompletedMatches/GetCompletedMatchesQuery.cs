using FifaTracker.Application.Sessions.Queries.GetSessionDetails;
using MediatR;

namespace FifaTracker.Application.Matches.GetCompletedMatches;

public record GetCompletedMatchesQuery(Guid SessionId) : IRequest<List<MatchDto>>;
