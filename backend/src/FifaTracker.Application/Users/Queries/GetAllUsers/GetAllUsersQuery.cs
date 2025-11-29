using MediatR;

namespace FifaTracker.Application.Users.Queries.GetAllUsers;

public record GetAllUsersQuery : IRequest<List<UserDto>>;

public record UserDto(
    Guid Id, 
    string Name, 
    DateTime CreatedAt,
    DateTime? LastSessionDate,
    int TotalSessionsCount,
    int TotalTimeSpentMinutes,
    CardStatus CardStatus
);

public enum CardStatus
{
    None,
    Yellow,
    Red
}
