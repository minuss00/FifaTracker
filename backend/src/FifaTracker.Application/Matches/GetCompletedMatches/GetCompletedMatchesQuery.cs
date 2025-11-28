using FifaTracker.Domain.Entities;
using MediatR;

namespace FifaTracker.Application.Matches.GetCompletedMatches;

public record GetCompletedMatchesQuery(Guid SessionId) : IRequest<List<Match>>;
