using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace FFTriadBuddy.UI
{
    public class BulkObservableCollection<T> : ObservableCollection<T>
    {
        private bool isNotifySuspended = false;
        public bool IsNotifySuspended => isNotifySuspended;

        private bool needsNotify = false;
        private int cachedCount = 0;

        protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
        {
            if (!isNotifySuspended)
            {
                base.OnCollectionChanged(e);
            }
            else
            {
                needsNotify = true;
            }
        }

        public void SuspendNotifies()
        {
            cachedCount = Items.Count;
            isNotifySuspended = true;
            needsNotify = false;
        }

        public void ResumeNotifies()
        {
            isNotifySuspended = false;
            if (needsNotify)
            {
                OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
            }
        }

        public void AddRange(IEnumerable<T> newItems)
        {
            SuspendNotifies();
            foreach (var item in newItems)
            {
                Add(item);
            }

            isNotifySuspended = false;
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, newItems, cachedCount));
        }
    }
}
