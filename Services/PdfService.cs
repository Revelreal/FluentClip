using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using UglyToad.PdfPig;

namespace FluentClip.Services;

public static class PdfService
{
    public static Task<string> ExtractTextFromPdfAsync(string filePath)
    {
        return Task.Run(() => ExtractTextFromPdf(filePath));
    }

    private static string ExtractTextFromPdf(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                return $"文件不存在: {filePath}";
            }

            var result = new StringBuilder();
            result.AppendLine($"📄 PDF文档: {Path.GetFileName(filePath)}");

            using var document = PdfDocument.Open(filePath);
            var pageCount = document.NumberOfPages;
            result.AppendLine($"📖 共 {pageCount} 页\n");
            result.AppendLine("=" .PadRight(50, '='));
            result.AppendLine();

            // 最多处理前10页，避免太长
            int maxPages = Math.Min(pageCount, 10);

            for (int i = 1; i <= maxPages; i++)
            {
                var page = document.GetPage(i);
                var text = page.Text;

                if (!string.IsNullOrWhiteSpace(text))
                {
                    result.AppendLine($"--- 第 {i} 页 ---");
                    result.AppendLine(text.Trim());
                    result.AppendLine();
                }
            }

            if (pageCount > 10)
            {
                result.AppendLine($"... (还有 {pageCount - 10} 页未显示)");
            }

            var fullText = result.ToString();
            if (string.IsNullOrWhiteSpace(fullText) || fullText.Length < 100)
            {
                return "PDF文档似乎没有可提取的文本内容，可能是因为它只包含图片扫描件。";
            }

            return fullText;
        }
        catch (Exception ex)
        {
            return $"读取PDF失败: {ex.Message}";
        }
    }

    public static string GetImageBase64(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                return $"文件不存在: {filePath}";
            }

            var bytes = File.ReadAllBytes(filePath);
            var base64 = Convert.ToBase64String(bytes);

            var ext = Path.GetExtension(filePath)?.ToLowerInvariant();
            var mimeType = ext switch
            {
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".gif" => "image/gif",
                ".bmp" => "image/bmp",
                ".webp" => "image/webp",
                ".tiff" or ".tif" => "image/tiff",
                _ => "image/png"
            };

            return $"data:{mimeType};base64,{base64}";
        }
        catch (Exception ex)
        {
            return $"读取图片失败: {ex.Message}";
        }
    }

    public static string GetImageInfo(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                return $"文件不存在: {filePath}";
            }

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(filePath);
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze();

            var width = bitmap.PixelWidth;
            var height = bitmap.PixelHeight;
            var format = bitmap.Format.ToString();

            var fileInfo = new FileInfo(filePath);
            var sizeKb = fileInfo.Length / 1024.0;

            var result = new StringBuilder();
            result.AppendLine($"🖼️ 图片文件: {Path.GetFileName(filePath)}");
            result.AppendLine($"📐 尺寸: {width} x {height} 像素");
            result.AppendLine($"💾 大小: {sizeKb:F1} KB");
            result.AppendLine($"📁 路径: {filePath}");

            return result.ToString();
        }
        catch (Exception ex)
        {
            return $"读取图片失败: {ex.Message}";
        }
    }
}
