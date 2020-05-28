using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using RemiBou.BlogPost.SignalR.Shared;

namespace RemiBou.BlogPost.SignalR.Server.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class HomeController : ControllerBase
    {
        private static int Counter = 0;
        private MediatR.IMediator _mediator;

        public HomeController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpPost("increment")]
        public async Task Post()
        {
            int val = Interlocked.Increment(ref Counter);
            await _mediator.Publish(new CounterIncremented(val));

        }

    }
}
