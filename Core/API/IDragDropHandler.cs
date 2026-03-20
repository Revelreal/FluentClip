using System;
using System.Windows;

namespace FluentClip.Core.API;

public interface IDragDropHandler
{
    string HandlerId { get; }
    int Priority { get; }
    bool CanHandle(DragDropInfo info);
    void HandleDragEnter(DragDropInfo info);
    void HandleDragLeave(DragDropInfo info);
    void HandleDrop(DragDropInfo info);
    DragDropEffects GetDropEffect(DragDropInfo info);
}

public class DragDropInfo
{
    public Point Position { get; init; }
    public IDataObject DataObject { get; init; } = null!;
    public DragDropEffects AllowedEffects { get; init; }
    public string[] FilePaths { get; init; } = Array.Empty<string>();
    public bool IsFromExternal { get; init; }
    public FrameworkElement? SourceElement { get; init; }
}
