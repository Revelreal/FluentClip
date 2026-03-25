using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FluentClip.Services
{
    public static class BubbleMessages
    {
        private static readonly Random _random = new();
        private static int _consecutiveCount = 0;
        private static string? _lastFileName = null;
        private static DateTime _lastMessageTime = DateTime.MinValue;
        private static readonly TimeSpan _minTimeBetweenOfftopic = TimeSpan.FromSeconds(8);

        private static string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            int order = 0;
            double size = bytes;
            while (size >= 1024 && order < sizes.Length - 1)
            {
                order++;
                size /= 1024;
            }
            return $"{size:0.##} {sizes[order]}";
        }

        private static string GetSizeDescription(long bytes)
        {
            if (bytes < 1024) return "小小的";
            if (bytes < 1024 * 100) return "不大";
            if (bytes < 1024 * 1024) return "一般大";
            if (bytes < 1024 * 1024 * 10) return "有点大";
            if (bytes < 1024 * 1024 * 100) return "好大";
            return "超大";
        }

        public static string GetSmartMessage(string? filePath, string? fileType)
        {
            _consecutiveCount++;
            var now = DateTime.Now;

            bool shouldOfftopic = _consecutiveCount > 3 && 
                                  (now - _lastMessageTime) > _minTimeBetweenOfftopic &&
                                  _random.Next(100) < 25;

            if (shouldOfftopic)
            {
                _consecutiveCount = 0;
                _lastMessageTime = now;
                return GetOfftopicMessage();
            }

            string? fileName = null;
            long fileSize = 0;
            if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
            {
                try
                {
                    var fileInfo = new FileInfo(filePath);
                    fileName = fileInfo.Name;
                    fileSize = fileInfo.Length;
                }
                catch
                {
                    fileName = Path.GetFileName(filePath);
                }
            }

            bool isNewFile = fileName != _lastFileName;
            _lastFileName = fileName;
            _lastMessageTime = now;

            return GenerateSmartMessage(fileType, fileName, fileSize, isNewFile);
        }

        private static string GenerateSmartMessage(string? fileType, string? fileName, long fileSize, bool isNewFile)
        {
            var parts = new List<string>();

            if (!string.IsNullOrEmpty(fileName))
            {
                if (isNewFile || _random.Next(100) < 60)
                {
                    string prefix = string.Format(BubbleMessageTemplates.FileNamePrefixes[_random.Next(BubbleMessageTemplates.FileNamePrefixes.Count)], fileName);
                    parts.Add(prefix);
                }
            }

            if (fileSize > 0)
            {
                if (_random.Next(100) < 40)
                {
                    string sizeDesc = GetSizeDescription(fileSize);
                    string sizePrefix = string.Format(BubbleMessageTemplates.SizePrefixes[_random.Next(BubbleMessageTemplates.SizePrefixes.Count)], sizeDesc);
                    parts.Add(sizePrefix);
                }
            }

            string baseGreeting;
            if (!string.IsNullOrEmpty(fileType) && BubbleMessageTemplates.TypeSpecificGreetings.TryGetValue(fileType.ToLower(), out var greetings))
            {
                baseGreeting = greetings[_random.Next(greetings.Count)];
            }
            else
            {
                baseGreeting = BubbleMessageTemplates.GeneralReactions[_random.Next(BubbleMessageTemplates.GeneralReactions.Count)];
            }

            parts.Add(baseGreeting);

            if (fileSize > 0 && _random.Next(100) < 25)
            {
                string sizeReaction = BubbleMessageTemplates.SizeReactions[_random.Next(BubbleMessageTemplates.SizeReactions.Count)];
                parts.Add(sizeReaction);
            }

            var result = string.Concat(parts);
            if (result.Length > 80 && _random.Next(100) < 50)
            {
                result = parts[parts.Count - 1];
            }

            return result;
        }

        private static string GetOfftopicMessage()
        {
            return BubbleMessageTemplates.OfftopicMessages[_random.Next(BubbleMessageTemplates.OfftopicMessages.Count)];
        }
    }
}
