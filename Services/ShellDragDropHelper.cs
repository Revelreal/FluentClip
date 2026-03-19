using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace FluentClip.Services;

public class ShellDragDropHelper
{
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern void SHAddToRecentDocs(uint uFlags, string pv);

    private const uint SHARD_PATHW = 0x00000003;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int RegisterClipboardFormat(string lpszFormat);

    private static readonly int CF_FileDrop = RegisterClipboardFormat("FileDrop");
    private static readonly int CF_FileGroupDescriptorW = RegisterClipboardFormat("FileGroupDescriptorW");

    public static object CreateDataObjectForDrag(string[] filePaths)
    {
        if (filePaths == null || filePaths.Length == 0)
            return null;

        try
        {
            var validFiles = new List<string>();
            foreach (var path in filePaths)
            {
                if (File.Exists(path) || Directory.Exists(path))
                {
                    validFiles.Add(path);
                }
            }

            if (validFiles.Count == 0)
                return null;

            var dataObject = new System.Windows.Forms.DataObject();
            
            var files = new StringCollection();
            files.AddRange(validFiles.ToArray());
            dataObject.SetFileDropList(files);

            try
            {
                var fileNames = new StringCollection();
                foreach (var path in validFiles)
                {
                    fileNames.Add(Path.GetFileName(path));
                }
                dataObject.SetData("FileNameW", fileNames);
            }
            catch { }

            try
            {
                var fgdBuffer = CreateFileGroupDescriptorW(validFiles.ToArray());
                if (fgdBuffer != null)
                {
                    dataObject.SetData("FileGroupDescriptorW", fgdBuffer);
                }
            }
            catch { }

            return dataObject;
        }
        catch
        {
            return null;
        }
    }

    private static byte[] CreateFileGroupDescriptorW(string[] filePaths)
    {
        try
        {
            int headerSize = 4;
            int entrySize = 296;
            int totalSize = headerSize + filePaths.Length * entrySize;

            byte[] buffer = new byte[totalSize];
            int offset = 0;

            Buffer.BlockCopy(BitConverter.GetBytes((uint)filePaths.Length), 0, buffer, offset, 4);
            offset += 4;

            foreach (var filePath in filePaths)
            {
                string fileName = Path.GetFileName(filePath);
                bool isDirectory = Directory.Exists(filePath);
                long fileSize = isDirectory ? 0 : new FileInfo(filePath).Length;
                DateTime modifiedDate = File.GetLastWriteTime(filePath);

                offset += 4;

                Buffer.BlockCopy(BitConverter.GetBytes((uint)fileName.Length), 0, buffer, offset, 4);
                offset += 4;

                offset += 4;

                byte[] fileNameBytes = Encoding.Unicode.GetBytes(fileName + "\0");
                int copyLen = Math.Min(fileNameBytes.Length, 260);
                Buffer.BlockCopy(fileNameBytes, 0, buffer, offset, copyLen);
                offset += 260;

                offset += 4;

                Buffer.BlockCopy(BitConverter.GetBytes(modifiedDate.ToFileTime()), 0, buffer, offset, 8);
                offset += 8;

                offset += 8;

                Buffer.BlockCopy(BitConverter.GetBytes(fileSize), 0, buffer, offset, 8);
                offset += 8;

                offset += 8;

                offset += 4;
            }

            return buffer;
        }
        catch
        {
            return null;
        }
    }

    public static System.Windows.Forms.DataObject CreateFileDropDataObject(string[] filePaths)
    {
        if (filePaths == null || filePaths.Length == 0)
            return new System.Windows.Forms.DataObject();

        try
        {
            var dataObject = new System.Windows.Forms.DataObject();
            
            var files = new StringCollection();
            foreach (var path in filePaths)
            {
                if (File.Exists(path) || Directory.Exists(path))
                {
                    files.Add(path);
                }
            }
            
            if (files.Count > 0)
            {
                dataObject.SetFileDropList(files);
            }

            return dataObject;
        }
        catch
        {
            return new System.Windows.Forms.DataObject();
        }
    }
}
