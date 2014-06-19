using System;
using System.Collections.Generic;
using System.Threading;
using Exceptionless.Extensions;
using Exceptionless.Models;
using Exceptionless.Models.Collections;

namespace Exceptionless.Storage {
    public class PersistedDictionary : SettingsDictionary {
        private readonly IFileStorage _fileStorage;
        private readonly IJsonSerializer _serializer;
        private readonly string _path;
        private readonly Timer _timer;
        private readonly int _delay;

        public PersistedDictionary(string path, IFileStorage fileStorage, IJsonSerializer serializer, int delay = 250) {
            _fileStorage = fileStorage;
            _serializer = serializer;
            _path = path;
            _delay = delay;
            Changed += OnChanged;
            _timer = new Timer(OnSaveTimer, null, -1, -1);
        }

        private void OnSaveTimer(object state) {
            _fileStorage.SaveObject(_path, this, _serializer);
            OnSaved();
        }

        private void OnChanged(object sender, ChangedEventArgs<KeyValuePair<string, string>> changedEventArgs) {
            _timer.Change(_delay, -1);
        }

        public event EventHandler<EventArgs> Saved;

        private void OnSaved() {
            if (Saved != null)
                Saved(this, EventArgs.Empty);
        }
    }
}
