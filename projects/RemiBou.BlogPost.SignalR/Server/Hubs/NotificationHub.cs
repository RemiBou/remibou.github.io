using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.SignalR;
using RemiBou.BlogPost.SignalR.Shared;

namespace RemiBou.BlogPost.SignalR.Server.Hubs
{
    public class NotificationHub : Hub
    {
    }

    public class HubNotificationHandler : INotificationHandler<CounterIncremented>
    {
        private readonly IHubContext<NotificationHub> _hubContext;

        public HubNotificationHandler(IHubContext<NotificationHub> hubContext)
        {
            _hubContext = hubContext;
        }
  
        public async Task Handle(CounterIncremented notification, CancellationToken cancellationToken)
        {
            JsonSerializerOptions options = new JsonSerializerOptions();
            options.Converters.Add(new NotificationJsonConverter());
            var json = JsonSerializer.Serialize(notification, options);
            await _hubContext.Clients.All.SendAsync("Notification",notification);
        }
    }
}
