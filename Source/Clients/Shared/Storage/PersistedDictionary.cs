using System;
using System.Collections.Generic;
using System.Threading;
using Exceptionless.Models;
using Exceptionless.Models.Collections;

namespace Exceptionless.Storage {
    public class PersistedDictionary : SettingsDictionary {
        private readonly IObjectStorage _objectStorage;
        private readonly string _path;
        private readonly Timer _timer;
        private readonly int _delay;

        public PersistedDictionary(string path, IObjectStorage objectStorage, IJsonSerializer serializer, int delay = 250) {
            _objectStorage = objectStorage;
            _path = path;
            _delay = delay;
            Changed += OnChanged;
            _timer = new Timer(OnSaveTimer, null, -1, -1);
        }

        public void Save() {
            _objectStorage.SaveObject(_path, this);
            OnSaved();
        }

        private void OnSaveTimer(object state) {
            Save();
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
