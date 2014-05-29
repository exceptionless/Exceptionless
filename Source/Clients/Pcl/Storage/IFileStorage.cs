using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Exceptionless.Storage {
    public interface IFileStorage {
        Task<Stream> GetFileContentsAsync(string path);
        Task SaveFileAsync(string path, Stream contents);
        Task RenameFileAsync(string oldpath, string newpath);
        Task DeleteFileAsync(string path);
        Task<IEnumerable<FileInfo>> GetFileListAsync(string spec = null);
    }

    public class FileInfo {
        public string Path { get; set; }
        public DateTime Created { get; set; }
        public DateTime Modified { get; set; }
        public long Size { get; set; }
    }
}
