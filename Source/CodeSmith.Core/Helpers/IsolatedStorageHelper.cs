using System;
using System.Collections.Generic;
using System.IO;
using System.IO.IsolatedStorage;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

namespace CodeSmith.Core.Helpers
{
    public class IsolatedStorageHelper<T>
    {
        private string _configFile;
        private string ConfigFile
        {
            get { return _configFile; }
        }

        private IsolatedStorageFile _storage;
        private IsolatedStorageFile Storage
        {
            get { return _storage; }
        }

        public IsolatedStorageHelper(string fileName) : this(IsolatedStorageScope.Assembly | IsolatedStorageScope.User, fileName) { }

        public IsolatedStorageHelper(IsolatedStorageScope scope, string fileName)
        {
            _storage = IsolatedStorageFile.GetStore(scope, null, null);
            _configFile = fileName;
        }

        public T Retrieve()
        {
            T result = default(T);

            string[] files = Storage.GetFileNames(ConfigFile);
            if (files == null || files.Length == 0)
                return result;

            using (IsolatedStorageFileStream stream = new IsolatedStorageFileStream(ConfigFile, FileMode.Open, FileAccess.Read, Storage))
            {
                if (null != stream && stream.Length > 0)
                {
                    result = SelfSerializer<T>.Current.XmlDeserialize(stream);
                }
            }

            return result;
        }

        public void Store(T item)
        {
            using (IsolatedStorageFileStream stream = new IsolatedStorageFileStream(ConfigFile, FileMode.Create, FileAccess.Write, Storage))
            {
                SelfSerializer<T>.Current.XmlSerialize(item, stream);
            }
        }

    }
}
