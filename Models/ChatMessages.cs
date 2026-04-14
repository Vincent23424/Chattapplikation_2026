using System;

namespace ChattModels
{
    // Grundklassen - Arv startar här
    public abstract class MessageBase
    {
        public string Sender { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }

    // En underklass för vanliga meddelanden
    public class TextMessage : MessageBase
    {
        public string Content { get; set; }
    }

    // En underklass för systemhändelser (t.ex. "User joined")
    public class SystemMessage : MessageBase
    {
        public string Action { get; set; }
    }
}