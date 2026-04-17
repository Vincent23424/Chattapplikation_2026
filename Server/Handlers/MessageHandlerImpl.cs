using System.Threading.Tasks;
using ChattModels;
using Serilog;

namespace Server.Handlers
{
    public class MessageHandlerImpl
    {
        private readonly ILogger _logger = Log.ForContext<MessageHandlerImpl>();

        public Task HandleAsync(MessageBase message)
        {
            // simple routing/handling based on type
            switch (message)
            {
                case TextMessage tm:
                    _logger.Information("Handled TextMessage from {Sender}", tm.Sender);
                    break;
                case PrivateMessage pm:
                    _logger.Information("Handled PrivateMessage from {Sender} to {Recipient}", pm.Sender, pm.Recipient);
                    break;
                case SystemMessage sm:
                    _logger.Information("Handled SystemMessage {Action}", sm.Action);
                    break;
            }
            return Task.CompletedTask;
        }
    }
}
