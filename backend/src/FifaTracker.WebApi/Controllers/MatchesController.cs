using FifaTracker.Application.Matches.Commands.CompleteMatch;
using FifaTracker.Application.Matches.Commands.CreateCustomMatch;
using FifaTracker.Application.Matches.Commands.DeleteMatch;
using FifaTracker.Application.Matches.Commands.UpdateMatchScore;
using FifaTracker.WebApi.Attributes;
using FifaTracker.WebApi.Requests;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace FifaTracker.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MatchesController : ControllerBase
{
    private readonly IMediator _mediator;

    public MatchesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost]
    public async Task<ActionResult<Guid>> CreateCustom([FromBody] CreateCustomMatchCommand command)
    {
        var matchId = await _mediator.Send(command);
        return Ok(matchId);
    }

    [HttpPost("complete")]
    [UpdateSessionActivity]
    public async Task<ActionResult<Guid>> CompleteMatch([FromBody] CompleteMatchCommand command)
    {
        var matchId = await _mediator.Send(command);
        return Ok(matchId);
    }

    [HttpPut("{id}/score")]
    [UpdateSessionActivity(IsMatchIdParameter = true)]
    public async Task<ActionResult> UpdateScore(Guid id, [FromBody] UpdateScoreRequest request)
    {
        await _mediator.Send(new UpdateMatchScoreCommand(id, request.Team1Score, request.Team2Score));
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(Guid id)
    {
        await _mediator.Send(new DeleteMatchCommand(id));
        return NoContent();
    }
}
