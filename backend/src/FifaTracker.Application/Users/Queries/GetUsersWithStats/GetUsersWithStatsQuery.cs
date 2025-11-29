using FifaTracker.Application.Users.Queries.GetAllUsers;
using MediatR;

namespace FifaTracker.Application.Users.Queries.GetUsersWithStats;

public record GetUsersWithStatsQuery : IRequest<List<UserDto>>;
