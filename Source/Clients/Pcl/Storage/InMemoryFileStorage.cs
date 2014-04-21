using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Exceptionless.Storage {
    public class InMemoryFileStorage : IFileStorage {
        public Task<Stream> GetFileAsync(string path) {
            throw new NotImplementedException();
        }

        public Task SaveFileAsync(string path, Stream contents) {
            throw new NotImplementedException();
        }

        public Task<IEnumerable<string>> GetFileListAsync(string path) {
            throw new NotImplementedException();
        }
    }
}
