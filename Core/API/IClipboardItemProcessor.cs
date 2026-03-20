using System.Windows.Media.Imaging;
using FluentClip.Models;

namespace FluentClip.Core.API;

public interface IClipboardItemProcessor
{
    string ProcessorId { get; }
    int Priority { get; }
    bool CanProcess(ClipboardItem item);
    ClipboardItem Process(ClipboardItem item);
    BitmapSource? GenerateThumbnail(string filePath, int width, int height);
}
