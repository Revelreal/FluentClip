using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace FluentClip.Events;

public interface IEventAggregator
{
    void Subscribe<TEvent>(EventHandler<TEvent> handler) where TEvent : EventArgs;
    void Unsubscribe<TEvent>(EventHandler<TEvent> handler) where TEvent : EventArgs;
    void Publish<TEvent>(TEvent eventArgs) where TEvent : EventArgs;
}

public class EventAggregator : IEventAggregator
{
    private readonly ConcurrentDictionary<Type, List<Delegate>> _handlers = new();
    private readonly object _lock = new();

    public void Subscribe<TEvent>(EventHandler<TEvent> handler) where TEvent : EventArgs
    {
        var eventType = typeof(TEvent);
        lock (_lock)
        {
            if (!_handlers.TryGetValue(eventType, out var handlers))
            {
                handlers = new List<Delegate>();
                _handlers[eventType] = handlers;
            }
            handlers.Add(handler);
        }
    }

    public void Unsubscribe<TEvent>(EventHandler<TEvent> handler) where TEvent : EventArgs
    {
        var eventType = typeof(TEvent);
        lock (_lock)
        {
            if (_handlers.TryGetValue(eventType, out var handlers))
            {
                handlers.Remove(handler);
            }
        }
    }

    public void Publish<TEvent>(TEvent eventArgs) where TEvent : EventArgs
    {
        var eventType = typeof(TEvent);
        List<Delegate>? handlersCopy = null;

        lock (_lock)
        {
            if (_handlers.TryGetValue(eventType, out var handlers))
            {
                handlersCopy = new List<Delegate>(handlers);
            }
        }

        if (handlersCopy != null)
        {
            foreach (var handler in handlersCopy)
            {
                try
                {
                    ((EventHandler<TEvent>)handler)(this, eventArgs);
                }
                catch { }
            }
        }
    }
}
