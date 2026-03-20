using System;
using System.Collections;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using FluentClip.Core.API;
using FluentClip.Factories;
using FluentClip.Models;

namespace FluentClip.Services;

public class DragDropService
{
    private readonly Window _window;
    private Border? _trashZone;
    private bool _isDragOverTrash;

    public event EventHandler<ClipboardItem>? OnFileDropped;
    public event EventHandler<ClipboardItem>? OnItemDroppedToTrash;

    public DragDropService(Window window)
    {
        _window = window;
    }

    public void SetTrashZone(Border trashZone)
    {
        _trashZone = trashZone;
    }

    public void HandleDragEnter(DragEventArgs e)
    {
        var files = GetFilePaths(e.Data);
        if (files.Length > 0)
        {
            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
        }
    }

    public void HandleDragLeave(DragEventArgs e)
    {
        e.Handled = true;
    }

    public void HandleDrop(DragEventArgs e)
    {
        var files = GetFilePaths(e.Data);
        var position = e.GetPosition(_window);

        if (_trashZone != null)
        {
            var trashBounds = VisualTreeHelper.GetDescendantBounds(_trashZone);
            var trashTopLeft = _trashZone.TransformToAncestor(_window).Transform(new Point(0, 0));
            var trashRect = new Rect(trashTopLeft, trashBounds.Size);

            if (trashRect.Contains(position))
            {
                HandleTrashDrop(files, e);
                return;
            }
        }

        if (files.Length > 0)
        {
            foreach (var file in files)
            {
                var item = ClipboardItemFactory.CreateFromFile(file);
                OnFileDropped?.Invoke(this, item);
            }
        }

        e.Handled = true;
    }

    private void HandleTrashDrop(string[] files, DragEventArgs e)
    {
        e.Effects = DragDropEffects.None;
    }

    private static string[] GetFilePaths(IDataObject dataObject)
    {
        if (dataObject.GetDataPresent(DataFormats.FileDrop))
        {
            var data = dataObject.GetData(DataFormats.FileDrop);
            if (data is string[] files)
                return files;
            if (data is IEnumerable enumerable)
                return enumerable.Cast<string>().ToArray();
        }
        return Array.Empty<string>();
    }

    public void StartDrag(Border border, ClipboardItem item, DataObject dataObject)
    {
        DragDrop.DoDragDrop(border, dataObject, DragDropEffects.Copy | DragDropEffects.Move);
    }

    public DataObject CreateDragData(ClipboardItem item)
    {
        var dataObject = new DataObject();

        if (item.ItemType == ClipboardItemType.Text && !string.IsNullOrEmpty(item.TextContent))
        {
            dataObject.SetData(DataFormats.Text, item.TextContent);
            dataObject.SetData(DataFormats.UnicodeText, item.TextContent);
        }
        else if (item.ItemType == ClipboardItemType.Image && item.ImageContent != null)
        {
            dataObject.SetData(DataFormats.Bitmap, item.ImageContent);
        }

        if (item.FilePaths != null && item.FilePaths.Length > 0)
        {
            dataObject.SetData(DataFormats.FileDrop, item.FilePaths);
        }

        return dataObject;
    }
}
