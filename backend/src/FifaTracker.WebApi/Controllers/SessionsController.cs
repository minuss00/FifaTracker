using FifaTracker.Application.Matches.GetCompletedMatches;
using FifaTracker.Application.Matches.GetPendingMatches;
using FifaTracker.Application.Sessions.Commands.AddUserToSession;
using FifaTracker.Application.Sessions.Commands.CreateSession;
using FifaTracker.Application.Sessions.Commands.EndSession;
using FifaTracker.Application.Sessions.Commands.PauseUserInSession;
using FifaTracker.Application.Sessions.Commands.ResumeUserInSession;
using FifaTracker.Application.Sessions.Queries.GetActiveSessions;
using FifaTracker.Application.Sessions.Queries.GetAllSessions;
using FifaTracker.Application.Sessions.Queries.GetSessionDetails;
using FifaTracker.WebApi.Attributes;
using FifaTracker.WebApi.Requests;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace FifaTracker.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SessionsController : ControllerBase
{
    private readonly IMediator _mediator;

    public SessionsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<ActionResult<List<SessionSummaryDto>>> GetAll()
    {
        var sessions = await _mediator.Send(new GetAllSessionsQuery());
        return Ok(sessions);
    }

    [HttpGet("active")]
    public async Task<ActionResult<List<SessionSummaryDto>>> GetActive()
    {
        var sessions = await _mediator.Send(new GetActiveSessionsQuery());
        return Ok(sessions);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<SessionDetailsDto>> GetById(Guid id)
    {
        var session = await _mediator.Send(new GetSessionDetailsQuery(id));
        return Ok(session);
    }

    [HttpPost]
    public async Task<ActionResult<Guid>> Create([FromBody] CreateSessionCommand command)
    {
        var sessionId = await _mediator.Send(command);
        return CreatedAtAction(nameof(GetById), new { id = sessionId }, sessionId);
    }

    [HttpPost("{id}/end")]
    public async Task<ActionResult> End(Guid id)
    {
        await _mediator.Send(new EndSessionCommand(id));
        return NoContent();
    }

    [HttpPost("{id}/users")]
    [UpdateSessionActivity]
    public async Task<ActionResult> AddUser(Guid id, [FromBody] AddUserRequest request)
    {
        await _mediator.Send(new AddUserToSessionCommand(id, request.UserId));
        return NoContent();
    }

    [HttpPost("{id}/users/{userId}/pause")]
    [UpdateSessionActivity]
    public async Task<IActionResult> PauseUser(Guid id, Guid userId)
    {
        await _mediator.Send(new PauseUserInSessionCommand(id, userId));
        return NoContent();
    }

    [HttpPost("{id}/users/{userId}/resume")]
    [UpdateSessionActivity]
    public async Task<IActionResult> ResumeUser(Guid id, Guid userId)
    {
        await _mediator.Send(new ResumeUserInSessionCommand(id, userId));
        return NoContent();
    }

    [HttpGet("{id}/pending-matches")]
    [UpdateSessionActivity]
    public async Task<ActionResult<List<Domain.Entities.Match>>> GetPendingMatches(Guid id)
    {
        var matches = await _mediator.Send(new GetPendingMatchesQuery(id));
        return Ok(matches);
    }

    [HttpGet("{id}/completed-matches")]
    [UpdateSessionActivity]
    public async Task<ActionResult<List<Domain.Entities.Match>>> GetCompletedMatches(Guid id)
    {
        var matches = await _mediator.Send(new GetCompletedMatchesQuery(id));
        return Ok(matches);
    }
}
