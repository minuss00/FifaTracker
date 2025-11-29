using FifaTracker.Domain.Entities;
using MediatR;

namespace FifaTracker.Application.Sessions.Queries.GetSessionDetails;

public record GetSessionDetailsQuery(Guid SessionId) : IRequest<SessionDetailsDto>;

public record SessionDetailsDto(
    Guid Id,
    string Name,
    DateTime StartDate,
    DateTime? EndDate,
    SessionStatus Status,
    FifaTracker.Domain.Entities.MatchType MatchType,
    List<SessionUserDto> Users,
    List<MatchDto> Matches
);

public record SessionUserDto(
    Guid UserId, 
    string UserName, 
    DateTime JoinedAt,
    bool IsActiveInSession,
    DateTime? PausedAt,
    TimeSpan TotalActiveTime
);

public record MatchDto(
    Guid Id,
    bool IsGenerated,
    bool IsCompleted,
    int? Team1Score,
    int? Team2Score,
    DateTime? PlayedAt,
    List<MatchPlayerDto> Team1Players,
    List<MatchPlayerDto> Team2Players,
    int TimesPlayed
);

public record MatchPlayerDto(Guid UserId, string UserName);
