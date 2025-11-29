using FifaTracker.Application.Users.Commands.CreateUser;
using FifaTracker.Application.Users.Commands.DeactivateUser;
using FifaTracker.Application.Users.Commands.ReactivateUser;
using FifaTracker.Application.Users.Commands.UpdateUser;
using FifaTracker.Application.Users.Queries.GetAllUsers;
using FifaTracker.Application.Users.Queries.GetInactiveUsers;
using FifaTracker.Application.Users.Queries.GetLeaderboard;
using FifaTracker.Application.Users.Queries.GetUsersWithStats;
using FifaTracker.WebApi.Requests;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace FifaTracker.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly IMediator _mediator;

    public UsersController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<ActionResult<List<UserDto>>> GetAll()
    {
        var users = await _mediator.Send(new GetAllUsersQuery());
        return Ok(users);
    }

    [HttpGet("with-stats")]
    public async Task<ActionResult<List<UserDto>>> GetWithStats()
    {
        var users = await _mediator.Send(new GetUsersWithStatsQuery());
        return Ok(users);
    }

    [HttpGet("inactive")]
    public async Task<ActionResult<List<InactiveUserDto>>> GetInactive()
    {
        var users = await _mediator.Send(new GetInactiveUsersQuery());
        return Ok(users);
    }

    [HttpGet("leaderboard")]
    public async Task<ActionResult<List<LeaderboardEntryDto>>> GetLeaderboard()
    {
        var leaderboard = await _mediator.Send(new GetLeaderboardQuery());
        return Ok(leaderboard);
    }

    [HttpPost]
    public async Task<ActionResult<Guid>> Create([FromBody] CreateUserCommand command)
    {
        var userId = await _mediator.Send(command);
        return CreatedAtAction(nameof(GetAll), new { id = userId }, userId);
    }

    [HttpPut("{id}")]
    public async Task<ActionResult> Update(Guid id, [FromBody] UpdateUserRequest request)
    {
        await _mediator.Send(new UpdateUserCommand(id, request.Name));
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> Deactivate(Guid id)
    {
        await _mediator.Send(new DeactivateUserCommand(id));
        return NoContent();
    }

    [HttpPost("{id}/reactivate")]
    public async Task<ActionResult> Reactivate(Guid id)
    {
        await _mediator.Send(new ReactivateUserCommand(id));
        return NoContent();
    }
}
