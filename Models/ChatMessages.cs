using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ChattModels
{
    // Grundklassen - Arv startar här
    public enum MessageType
    {
        Text,
        System,
        Private
    }

    public abstract class MessageBase
    {
        // Unikt id för varje meddelande - användbart för loggning och spårning
        public Guid MessageId { get; init; } = Guid.NewGuid();

        // Vem skickade meddelandet
        public string Sender { get; set; }

        // Tidsstämpel (UTC) så att server/klient kan visa korrekt tidzon vid behov
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        // Typ av meddelande (text, system, privat)
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public MessageType Type { get; protected set; }

        protected MessageBase()
        {
            // default handled by initializers
        }

        // Hjälpmetod för JSON-serialisering
        public string ToJson()
        {
            var options = new JsonSerializerOptions
            {
                Converters = { new JsonStringEnumConverter() },
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            return JsonSerializer.Serialize(this, options);
        }

        public static T? FromJson<T>(string json) where T : MessageBase
        {
            var options = new JsonSerializerOptions
            {
                Converters = { new JsonStringEnumConverter() },
                PropertyNameCaseInsensitive = true
            };
            return JsonSerializer.Deserialize<T>(json, options);
        }

        // Non-generic factory that inspects the JSON "type" and deserializes to the right subclass
        public static MessageBase? FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;

            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("type", out var typeProp))
            {
                // fallback: try TextMessage
                return FromJson<TextMessage>(json);
            }

            var typeStr = typeProp.GetString();
            var options = new JsonSerializerOptions
            {
                Converters = { new JsonStringEnumConverter() },
                PropertyNameCaseInsensitive = true
            };

            return typeStr switch
            {
                "Text" => JsonSerializer.Deserialize<TextMessage>(json, options),
                "System" => JsonSerializer.Deserialize<SystemMessage>(json, options),
                "Private" => JsonSerializer.Deserialize<PrivateMessage>(json, options),
                _ => JsonSerializer.Deserialize<TextMessage>(json, options)
            };
        }

        public override string ToString()
        {
            // Show local time for readability
            var local = Timestamp.ToLocalTime();
            return $"[{local:yyyy-MM-dd HH:mm:ss}] ({Type}) {Sender}:";
        }
    }

    // En underklass för vanliga meddelanden
    public class TextMessage : MessageBase
    {
        public string Content { get; set; }

        public TextMessage()
        {
            Type = MessageType.Text;
        }

        public TextMessage(string sender, string content)
        {
            if (string.IsNullOrWhiteSpace(sender)) throw new ArgumentException("Sender must be provided", nameof(sender));
            if (string.IsNullOrWhiteSpace(content)) throw new ArgumentException("Content must not be empty", nameof(content));

            Sender = sender;
            Content = content;
            Type = MessageType.Text;
            Timestamp = DateTime.UtcNow;
        }

        public override string ToString()
        {
            return base.ToString() + $" {Content}";
        }
    }

    // En underklass för systemhändelser (t.ex. "User joined")
    public class SystemMessage : MessageBase
    {
        public string Action { get; set; }

        public SystemMessage()
        {
            Type = MessageType.System;
            Sender = "System";
        }

        public SystemMessage(string action)
        {
            if (string.IsNullOrWhiteSpace(action)) throw new ArgumentException("Action must be provided", nameof(action));
            Action = action;
            Type = MessageType.System;
            Sender = "System";
            Timestamp = DateTime.UtcNow;
        }

        public override string ToString()
        {
            return base.ToString() + $" {Action}";
        }
    }

    // En underklass för privata meddelanden
    public class PrivateMessage : MessageBase
    {
        public string Recipient { get; set; }
        public string Content { get; set; }

        public PrivateMessage()
        {
            Type = MessageType.Private;
        }

        public PrivateMessage(string sender, string recipient, string content)
        {
            if (string.IsNullOrWhiteSpace(sender)) throw new ArgumentException("Sender must be provided", nameof(sender));
            if (string.IsNullOrWhiteSpace(recipient)) throw new ArgumentException("Recipient must be provided", nameof(recipient));
            if (string.IsNullOrWhiteSpace(content)) throw new ArgumentException("Content must not be empty", nameof(content));

            Sender = sender;
            Recipient = recipient;
            Content = content;
            Type = MessageType.Private;
            Timestamp = DateTime.UtcNow;
        }

        public override string ToString()
        {
            return base.ToString() + $" (to {Recipient}) {Content}";
        }
    }
}