using System.Collections.Concurrent;
using System.Threading.Channels;
using codeMRI.Agents.Models;
using Microsoft.Extensions.Logging;

namespace codeMRI.Agents.Services;

public class AgentMessageBus
{
    private readonly Channel<AgentMessage> _channel;
    private readonly ILogger<AgentMessageBus> _logger;
    private readonly ConcurrentDictionary<string, List<Func<AgentMessage, Task>>> _subscribers;

    public AgentMessageBus(ILogger<AgentMessageBus> logger)
    {
        _channel = Channel.CreateUnbounded<AgentMessage>();
        _subscribers = new ConcurrentDictionary<string, List<Func<AgentMessage, Task>>>();
        _logger = logger;

        // Start processing loop
        Task.Run(ProcessMessagesAsync);
    }

    public async Task PublishAsync(AgentMessage message)
    {
        await _channel.Writer.WriteAsync(message);
    }

    public void Subscribe(string messageType, Func<AgentMessage, Task> handler)
    {
        _subscribers.AddOrUpdate(messageType,
            _ => new List<Func<AgentMessage, Task>> { handler },
            (_, handlers) =>
            {
                lock (handlers)
                {
                    handlers.Add(handler);
                }

                return handlers;
            });
    }

    private async Task ProcessMessagesAsync()
    {
        await foreach (var message in _channel.Reader.ReadAllAsync())
            try
            {
                if (_subscribers.TryGetValue(message.MessageType, out var handlers))
                {
                    List<Func<AgentMessage, Task>> handlersCopy;
                    lock (handlers)
                    {
                        handlersCopy = handlers.ToList();
                    }

                    foreach (var handler in handlersCopy)
                        try
                        {
                            await handler(message);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error handling message {MessageType}", message.MessageType);
                        }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing message from channel");
            }
    }
}