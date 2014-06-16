using System;
using System.Collections.Generic;

namespace Exceptionless.Storage {
    public interface IFileStorage : IDisposable {
        string GetFileContents(string path);
        void SaveFile(string path, string contents);
        void RenameFile(string oldpath, string newpath);
        void DeleteFile(string path);
        IEnumerable<FileInfo> GetFileList(string spec = null);
    }

    public class FileInfo {
        public string Path { get; set; }
        public DateTime Created { get; set; }
        public DateTime Modified { get; set; }
        public long Size { get; set; }
    }
}
