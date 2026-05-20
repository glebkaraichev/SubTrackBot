using MediatR;
using Microsoft.AspNetCore.Mvc;
using SubTrack.Application.Subscriptions.Commands;

namespace SubTrack.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SubscriptionsController : ControllerBase
{
    private readonly IMediator _mediator;

    public SubscriptionsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost]
    public async Task<ActionResult<Guid>> Create(CreateSubscriptionCommand command)
    {
        var result = await _mediator.Send(command);
        return Ok(result);
    }
}