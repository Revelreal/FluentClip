using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using FluentClip.Core.API;
using FluentClip.Events;

namespace FluentClip.Core;

public class PluginManager : IPluginContext
{
    private readonly ConcurrentDictionary<string, IPlugin> _plugins = new();
    private readonly ConcurrentDictionary<Type, object> _services = new();
    private readonly ConcurrentDictionary<string, List<IClipboardItemProcessor>> _clipboardProcessors = new();
    private readonly ConcurrentDictionary<string, List<IDragDropHandler>> _dragDropHandlers = new();
    private readonly ConcurrentDictionary<string, List<IThumbnailGenerator>> _thumbnailGenerators = new();
    private readonly IEventAggregator _eventAggregator;
    private bool _isInitialized;

    public PluginManager(IEventAggregator eventAggregator)
    {
        _eventAggregator = eventAggregator;
    }

    public void Initialize()
    {
        if (_isInitialized) return;
        _isInitialized = true;
        LoadInternalServices();
    }

    private void LoadInternalServices()
    {
    }

    public void RegisterService(Type serviceType, object instance)
    {
        _services[serviceType] = instance;
    }

    public T GetService<T>() where T : class
    {
        if (_services.TryGetValue(typeof(T), out var service))
        {
            return (T)service;
        }
        throw new InvalidOperationException($"Service {typeof(T).Name} is not registered.");
    }

    public void SubscribeToEvent<TEvent>(EventHandler<TEvent> handler) where TEvent : EventArgs
    {
        _eventAggregator.Subscribe(handler);
    }

    public void PublishEvent<TEvent>(TEvent eventArgs) where TEvent : EventArgs
    {
        _eventAggregator.Publish(eventArgs);
    }

    public void RegisterClipboardItemProcessor(IClipboardItemProcessor processor)
    {
        var key = processor.GetType().FullName ?? processor.ProcessorId;
        if (!_clipboardProcessors.ContainsKey(key))
        {
            _clipboardProcessors[key] = new List<IClipboardItemProcessor>();
        }
        _clipboardProcessors[key].Add(processor);
    }

    public void RegisterDragDropHandler(IDragDropHandler handler)
    {
        var key = handler.GetType().FullName ?? handler.HandlerId;
        if (!_dragDropHandlers.ContainsKey(key))
        {
            _dragDropHandlers[key] = new List<IDragDropHandler>();
        }
        _dragDropHandlers[key].Add(handler);
    }

    public void RegisterThumbnailGenerator(IThumbnailGenerator generator)
    {
        var key = generator.GetType().FullName ?? generator.GeneratorId;
        if (!_thumbnailGenerators.ContainsKey(key))
        {
            _thumbnailGenerators[key] = new List<IThumbnailGenerator>();
        }
        _thumbnailGenerators[key].Add(generator);
    }

    public void AddMenuItem(string menuId, string header, RoutedEventHandler clickHandler)
    {
    }

    public void AddContextMenuItem(string targetMenuId, string header, RoutedEventHandler clickHandler)
    {
    }

    public void LoadPlugin(IPlugin plugin)
    {
        plugin.Initialize(this);
        _plugins[plugin.Id] = plugin;
    }

    public void UnloadPlugin(string pluginId)
    {
        if (_plugins.TryRemove(pluginId, out var plugin))
        {
            plugin.Shutdown();
        }
    }

    public IEnumerable<IPlugin> GetLoadedPlugins() => _plugins.Values;

    public IClipboardItemProcessor? GetClipboardProcessor(Models.ClipboardItem item)
    {
        foreach (var kvp in _clipboardProcessors)
        {
            foreach (var processor in kvp.Value)
            {
                if (processor.CanProcess(item))
                {
                    return processor;
                }
            }
        }
        return null;
    }

    public IDragDropHandler? GetDragDropHandler(DragDropInfo info)
    {
        foreach (var kvp in _dragDropHandlers)
        {
            foreach (var handler in kvp.Value)
            {
                if (handler.CanHandle(info))
                {
                    return handler;
                }
            }
        }
        return null;
    }

    public IThumbnailGenerator? GetThumbnailGenerator(string filePath)
    {
        foreach (var kvp in _thumbnailGenerators)
        {
            foreach (var generator in kvp.Value)
            {
                if (generator.CanGenerate(filePath))
                {
                    return generator;
                }
            }
        }
        return null;
    }
}
