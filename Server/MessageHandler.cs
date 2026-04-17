using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ChattModels;

namespace Server
{
    // Generic message handler interface
    public interface IMessageHandler<T> where T : MessageBase
    {
        Task HandleAsync(T message);
    }

    // Broadcaster that handles any MessageBase and broadcasts to registered sinks
    public class MessageBroadcaster
    {
        private readonly Func<List<object>> _snapshotProvider;

        public MessageBroadcaster(Func<List<object>> snapshotProvider)
        {
            _snapshotProvider = snapshotProvider;
        }

        public Task BroadcastAsync(MessageBase message)
        {
            // currently synchronous write per client; keep signature Task for future async sinks
            var snapshot = _snapshotProvider().ToList();
            var payload = message.ToJson();
            foreach (dynamic state in snapshot)
            {
                try
                {
                    if (!state.Client.Connected) continue;
                    lock (state.WriteLock)
                    {
                        state.Writer.WriteLine(payload);
                    }
                }
                catch
                {
                    // ignore per-client failures here
                }
            }
            return Task.CompletedTask;
        }
    }

    // Simple generic repository (in-memory) for demonstration
    public interface IRepository<T>
    {
        void Add(T item);
        IEnumerable<T> GetAll();
    }

    public class InMemoryRepository<T> : IRepository<T>
    {
        private readonly List<T> _items = new List<T>();
        public void Add(T item) => _items.Add(item);
        public IEnumerable<T> GetAll() => _items.ToList();
    }
}
