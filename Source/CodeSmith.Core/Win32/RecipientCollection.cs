using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.InteropServices;

namespace CodeSmith.Core.Win32
{
    /// <summary>
    /// A collection of recipients for a mail message.
    /// </summary>
    public class RecipientCollection : Collection<Recipient>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RecipientCollection"/> class.
        /// </summary>
        public RecipientCollection()
        { }

        /// <summary>
        /// Initializes a new instance of the <see cref="RecipientCollection"/> class.
        /// </summary>
        /// <param name="list">The list that is wrapped by the new collection.</param>
        /// <exception cref="T:System.ArgumentNullException">
        /// 	<paramref name="list"/> is <c>null</c>.
        /// </exception>
        public RecipientCollection(IList<Recipient> list)
            : base(list)
        { }

        /// <summary>
        /// Adds a new recipient with the specified email address to this collection.
        /// </summary>
        /// <param name="emailAddress">The email address.</param>
        public void Add(string emailAddress)
        {
            Add(new Recipient(emailAddress));
        }

        /// <summary>
        /// Adds a new recipient with the specified email address and display name to this collection.
        /// </summary>
        /// <param name="emailAddress">The email address.</param>
        /// <param name="displayName">The display name.</param>
        public void Add(string emailAddress, string displayName)
        {
            Add(new Recipient(emailAddress, displayName));
        }

        /// <summary>
        /// Adds a new recipient with the specified email address and recipient type to this collection.
        /// </summary>
        /// <param name="emailAddress">The email address.</param>
        /// <param name="recipientType">Type of the recipient.</param>
        public void Add(string emailAddress, MailDialog.RecipientType recipientType)
        {
            Add(new Recipient(emailAddress, recipientType));
        }

        /// <summary>
        /// Adds a new recipient with the specified email address, display name and recipient type to this collection.
        /// </summary>
        /// <param name="emailAddress">The email address.</param>
        /// <param name="displayName">The display name.</param>
        /// <param name="recipientType">Type of the recipient.</param>
        public void Add(string emailAddress, string displayName, MailDialog.RecipientType recipientType)
        {
            Add(new Recipient(emailAddress, displayName, recipientType));
        }

        internal RecipientCollectionHandle GetHandle()
        {
            return new RecipientCollectionHandle(this);
        }

        internal class RecipientCollectionHandle : CriticalHandle
        {
            private int _count;

            public RecipientCollectionHandle(ICollection<Recipient> list)
                : base(IntPtr.Zero)
            {
                if (list == null)
                    throw new ArgumentNullException("list");

                _count = list.Count;

                if (_count == 0)
                    return;

                // allocate enough memory to hold all recipients
                Type type = typeof(Mapi.MapiRecipDesc);
                int size = Marshal.SizeOf(type);

                SetHandle(Marshal.AllocHGlobal(_count * size));

                // place all interop recipients into the memory just allocated
                int ptr = (int)handle;
                foreach (Recipient recipient in list)
                {
                    var recipDesc = recipient.GetMapiRecipDesc();

                    // stick it in the memory block
                    Marshal.StructureToPtr(recipDesc, (IntPtr)ptr, false);
                    ptr += size;
                }
            }

            protected override bool ReleaseHandle()
            {
                if (IsInvalid)
                    return true;

                Type type = typeof(Mapi.MapiRecipDesc);
                int size = Marshal.SizeOf(type);

                // destroy all the structures in the memory area
                int ptr = (int)handle;
                for (int i = 0; i < _count; i++)
                {
                    Marshal.DestroyStructure((IntPtr)ptr, type);
                    ptr += size;
                }

                // free the memory
                Marshal.FreeHGlobal(handle);

                SetHandle(IntPtr.Zero);
                _count = 0;

                return true;
            }

            public override bool IsInvalid
            {
                get { return (handle == IntPtr.Zero); }
            }

            public static implicit operator IntPtr(RecipientCollectionHandle recipientCollectionHandle)
            {
                return recipientCollectionHandle.handle;
            }
        }

    }
}