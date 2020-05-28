using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using MediatR;

namespace RemiBou.BlogPost.SignalR.Shared
{
    public abstract class SerializedNotification : INotification
    {
        public string NotificationType
        {
            get
            {
                return this.GetType().Name;
            }
            set{}
        }
    }
    public class CounterIncremented : SerializedNotification
    {
        public int Counter { get; set; }

        public CounterIncremented(int val)
        {
            this.Counter = val; 
        }

        public CounterIncremented()
        {
        }

        public override string ToString()
        {
            return $"Counter incremented ! new value {Counter}";
        }
    }
   
    public class NotificationJsonConverter : JsonConverter<SerializedNotification>
    {
        private readonly IEnumerable<Type> _types;

        public NotificationJsonConverter()
        {
            var type = typeof(SerializedNotification);
            _types = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(s => s.GetTypes())
                .Where(p => type.IsAssignableFrom(p) && p.IsClass && !p.IsAbstract)
                .ToList();
        }

        public override SerializedNotification Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException();
            }

            using (var jsonDocument = JsonDocument.ParseValue(ref reader))
            {   
                if (!jsonDocument.RootElement.TryGetProperty("notificationType", out var typeProperty))
                {
                    throw new JsonException();
                }
                var type = _types.FirstOrDefault(x => x.Name == typeProperty.GetString());
                if (type == null)
                {
                    throw new JsonException();
                }

                var jsonObject = jsonDocument.RootElement.GetRawText(); 
                var result = (SerializedNotification)JsonSerializer.Deserialize(jsonObject, type, options);
                return result;
            }
        }

        public override void Write(Utf8JsonWriter writer, SerializedNotification value, JsonSerializerOptions options)
        {
            JsonSerializer.Serialize(writer, (object)value, options);
        }
    }
}