using System;
using System.Windows;

namespace FluentClip.Core.API;

public interface IPlugin
{
    string Id { get; }
    string Name { get; }
    string Version { get; }
    string Author { get; }
    string Description { get; }

    void Initialize(IPluginContext context);
    void Shutdown();
}

public interface IPluginContext
{
    void RegisterService(Type serviceType, object instance);
    T GetService<T>() where T : class;
    void SubscribeToEvent<TEvent>(EventHandler<TEvent> handler) where TEvent : EventArgs;
    void PublishEvent<TEvent>(TEvent eventArgs) where TEvent : EventArgs;
    void RegisterClipboardItemProcessor(IClipboardItemProcessor processor);
    void RegisterDragDropHandler(IDragDropHandler handler);
    void RegisterThumbnailGenerator(IThumbnailGenerator generator);
    void AddMenuItem(string menuId, string header, RoutedEventHandler clickHandler);
    void AddContextMenuItem(string targetMenuId, string header, RoutedEventHandler clickHandler);
}
