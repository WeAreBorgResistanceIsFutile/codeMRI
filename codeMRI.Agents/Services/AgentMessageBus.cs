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

    public void Unsubscribe(string messageType, Func<AgentMessage, Task> handler)
    {
        if (_subscribers.TryGetValue(messageType, out var handlers))
            lock (handlers)
            {
                handlers.Remove(handler);
            }
    }

    private async Task ProcessMessagesAsync()
    {
        await foreach (var message in _channel.Reader.ReadAllAsync())
            try
            {
                // Collect all handlers for this specific message type
                var allHandlers = new List<Func<AgentMessage, Task>>();

                // Add specific message type handlers
                if (_subscribers.TryGetValue(message.MessageType, out var handlers))
                    lock (handlers)
                    {
                        allHandlers.AddRange(handlers.ToList());
                    }

                // Add wildcard handlers (subscribe to all message types)
                if (_subscribers.TryGetValue("*", out var wildcardHandlers))
                    lock (wildcardHandlers)
                    {
                        allHandlers.AddRange(wildcardHandlers.ToList());
                    }

                // Invoke all handlers
                foreach (var handler in allHandlers)
                    try
                    {
                        await handler(message);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error handling message {MessageType}", message.MessageType);
                    }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing message from channel");
            }
    }
}