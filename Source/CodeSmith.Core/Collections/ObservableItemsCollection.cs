using System;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace CodeSmith.Core.Collections {
    public class NotifyCollectionItemChangeEventArgs : PropertyChangedEventArgs {
        public int Index { get; set; }

        public NotifyCollectionItemChangeEventArgs(int index, string propertyName)
            : base(propertyName) {
            Index = index;
        }
    }

    public class ObservableItemsCollection<T> : System.Collections.ObjectModel.ObservableCollection<T> where T : class, INotifyPropertyChanged {
        public event EventHandler<NotifyCollectionItemChangeEventArgs> ItemChanged;

        protected override void ClearItems() {
            foreach (var item in Items)
                item.PropertyChanged -= ItemPropertyChanged;
            
            base.ClearItems();
        }

        protected override void SetItem(int index, T item) {
            Items[index].PropertyChanged -= ItemPropertyChanged;
            base.SetItem(index, item);
            Items[index].PropertyChanged += ItemPropertyChanged;
        }

        protected override void RemoveItem(int index) {
            Items[index].PropertyChanged -= ItemPropertyChanged;
            base.RemoveItem(index);
        }

        protected override void InsertItem(int index, T item) {
            base.InsertItem(index, item);
            item.PropertyChanged += ItemPropertyChanged;
        }

        private void ItemPropertyChanged(object sender, PropertyChangedEventArgs e) {
            T changedItem = sender as T;
            int index = IndexOf(changedItem);
            if (index >= 0)
                OnItemChanged(IndexOf(changedItem), e.PropertyName);
        }

        private void OnItemChanged(int index, string propertyName) {
            if (ItemChanged != null)
                ItemChanged(this, new NotifyCollectionItemChangeEventArgs(index, propertyName));
        }
    }
}
