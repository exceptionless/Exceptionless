using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Exceptionless.Storage {
    public interface IFileStorage {
        Task<Stream> GetFileAsync(string path);
        Task SaveFileAsync(string path, Stream contents);
        Task<IEnumerable<string>> GetFileListAsync(string path);
    }
}
