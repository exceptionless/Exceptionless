using System;
using System.Collections.Generic;

namespace Exceptionless.Storage {
    public interface IObjectStorage : IDisposable {
        T GetObject<T>(string path) where T : class;
        ObjectInfo GetObjectInfo(string path);
        bool Exists(string path);
        bool SaveObject<T>(string path, T value) where T : class;
        bool RenameObject(string oldpath, string newpath);
        bool DeleteObject(string path);
        IEnumerable<ObjectInfo> GetObjectList(string searchPattern = null, int? limit = null, DateTime? maxCreatedDate = null);
    }

    public class ObjectInfo {
        public string Path { get; set; }
        public DateTime Created { get; set; }
        public DateTime Modified { get; set; }
    }
}
