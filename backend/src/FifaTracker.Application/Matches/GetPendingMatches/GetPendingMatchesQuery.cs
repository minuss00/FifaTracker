using FifaTracker.Domain.Entities;
using MediatR;

namespace FifaTracker.Application.Matches.GetPendingMatches;

public record GetPendingMatchesQuery(Guid SessionId) : IRequest<List<Match>>;
