using System;
using System.Collections.Generic;

namespace Exceptionless.Storage {
    public interface IFileStorage : IDisposable {
        string GetFileContents(string path);
        FileInfo GetFileInfo(string path);
        bool Exists(string path);
        bool SaveFile(string path, string contents);
        bool RenameFile(string oldpath, string newpath);
        bool DeleteFile(string path);
        IEnumerable<FileInfo> GetFileList(string searchPattern = null, int? limit = null);
    }

    public class FileInfo {
        public string Path { get; set; }
        public DateTime Created { get; set; }
        public DateTime Modified { get; set; }
        public long Size { get; set; }
    }
}
