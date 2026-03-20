using System.Windows.Media.Imaging;

namespace FluentClip.Core.API;

public interface IThumbnailGenerator
{
    string GeneratorId { get; }
    int Priority { get; }
    string[] SupportedExtensions { get; }
    bool CanGenerate(string filePath);
    BitmapSource? Generate(string filePath, int width, int height);
}
