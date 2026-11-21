using Microsoft.AspNetCore.Mvc;
using Reviews.Application.Discussions;
using Reviews.Application.Discussions.Commands.CreateDiscussion;
using Reviews.Application.Discussions.Queries.GetDiscussionById;

namespace Reviews.WebApi.Controllers;

[Route("api/discussions")]
public class DiscussionsController : ApiControllerBase
{
    [HttpGet("{id}")]
    [ProducesResponseType<DiscussionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DiscussionDto>> GetById(string id, CancellationToken ct)
    {
        var discussion = await Mediator.Send(new GetDiscussionByIdQuery(id), ct);
        SetETag(discussion.Version);
        return Ok(discussion);
    }

    [HttpPost]
    [ProducesResponseType<string>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<string>> Create(CreateDiscussionCommand command, CancellationToken ct)
    {
        var id = await Mediator.Send(command, ct);
        return CreatedAtAction(nameof(GetById), new { id }, id);
    }
}
