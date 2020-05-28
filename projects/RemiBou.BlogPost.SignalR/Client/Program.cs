using System;
using System.Net.Http;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Text;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.Components;
using Blazor.Extensions.Logging;
using RemiBou.BlogPost.SignalR.Shared;
using System.Linq;
using MediatR;
using System.Threading;

namespace RemiBou.BlogPost.SignalR.Client
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebAssemblyHostBuilder.CreateDefault(args);
            builder.RootComponents.Add<App>("app");
            builder.Services.AddTransient(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

            var app = builder.Build();
            var navigationManager = app.Services.GetRequiredService<NavigationManager>();
            var hubConnection = new HubConnectionBuilder()
                        .WithUrl(navigationManager.ToAbsoluteUri("/notifications"))
                        .AddJsonProtocol(o => o.PayloadSerializerOptions.Converters.Add(new NotificationJsonConverter()))
                        .Build();

            hubConnection.On<SerializedNotification>("Notification", async (notificationJson) =>
            {
                await DynamicNotificationHandlers.Publish(notificationJson);
            });

            await hubConnection.StartAsync();
            await app.RunAsync();
        }
    }

    public static class DynamicNotificationHandlers
    {
        private static Dictionary<Type, List<(object,Func<SerializedNotification, Task>)>> _handlers = new Dictionary<Type, List<(object, Func<SerializedNotification, Task>)>>();
        public static void Register<T>(INotificationHandler<T> handler) where T : SerializedNotification
        {
            lock (_handlers)
            {
                var handlerInterfaces = handler
                    .GetType()
                    .GetInterfaces()
                    .Where(x =>
                        x.IsGenericType &&
                        x.GetGenericTypeDefinition() == typeof(INotificationHandler<>))
                    .ToList();
                foreach (var item in handlerInterfaces)
                {
                    var notificationType = item.GenericTypeArguments.First();
                    if(!_handlers.TryGetValue(notificationType,out var handlers)){
                        handlers = new List<(object, Func<SerializedNotification, Task>)>();
                        _handlers.Add(notificationType,handlers);
                    }
                    handlers.Add((handler, async s => await handler.Handle((T)s, default(CancellationToken))));
                }
            }
        }
        public static void Unregister<T>(INotificationHandler<T> handler) where T : SerializedNotification
        {
            lock (_handlers)
            {
                foreach (var item in _handlers)
                {
                    item.Value.RemoveAll(h => h.Item1.Equals(handler));
                }
            }
        }
        public static async Task Publish(SerializedNotification notification)
        {
            try
            {
                var notificationType = notification.GetType();
                if(_handlers.TryGetValue(notificationType, out var filtered)){
                    foreach (var item in filtered)
                    { 
                        await item.Item2(notification);
                    }
                }

            }
            catch (System.Exception e)
            {
                Console.Error.WriteLine(e + " " + e.StackTrace);

                throw;
            }
        }
    }
}
