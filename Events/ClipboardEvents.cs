using System;
using FluentClip.Models;

namespace FluentClip.Events;

public class ClipboardItemCreatedEvent : EventArgs
{
    public ClipboardItem Item { get; init; } = null!;
}

public class ClipboardItemDeletedEvent : EventArgs
{
    public ClipboardItem Item { get; init; } = null!;
}

public class ClipboardItemCopiedEvent : EventArgs
{
    public ClipboardItem Item { get; init; } = null!;
}

public class DragEnterEvent : EventArgs
{
    public string[] FilePaths { get; init; } = Array.Empty<string>();
}

public class DragLeaveEvent : EventArgs { }

public class DropEvent : EventArgs
{
    public string[] FilePaths { get; init; } = Array.Empty<string>();
}

public class ToastRequestEvent : EventArgs
{
    public string Message { get; init; } = string.Empty;
    public ToastType Type { get; init; } = ToastType.Info;
}

public enum ToastType
{
    Info,
    Success,
    Warning,
    Error
}
