using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Runtime.InteropServices;

namespace CodeSmith.Core.Win32
{
    /// <summary>
    /// A collection class for attachments
    /// </summary>
    public class AttachmentCollection : Collection<string>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AttachmentCollection"/> class.
        /// </summary>
        public AttachmentCollection()
        {}

        /// <summary>
        /// Initializes a new instance of the <see cref="AttachmentCollection"/> class.
        /// </summary>
        /// <param name="list">The list that is wrapped by the new collection.</param>
        /// <exception cref="T:System.ArgumentNullException">
        /// 	<paramref name="list"/> is null.
        /// </exception>
        public AttachmentCollection(IList<string> list) : base(list)
        {}

        internal AttachmentCollectionHandle GetHandle()
        {
            return new AttachmentCollectionHandle(this);
        }

        internal class AttachmentCollectionHandle : CriticalHandle
        {
            private int _count;

            public AttachmentCollectionHandle(ICollection<string> files)
                : base(IntPtr.Zero)
            {
                if (files == null)
                    throw new ArgumentNullException("files");
                
                _count = files.Count;
                if (_count == 0)
                    return;

                // allocate enough memory to hold all files
                var type = typeof(Mapi.MapiFileDescriptor);
                int size = Marshal.SizeOf(type);

                SetHandle(Marshal.AllocHGlobal(_count * size));

                // place all interop files into the memory just allocated
                int ptr = (int)handle;
                foreach (var file in files)
                {
                    var fileDescriptor = new Mapi.MapiFileDescriptor();
                    fileDescriptor.Position = -1;
                    fileDescriptor.FileName = Path.GetFileName(file);
                    fileDescriptor.PathName = Path.GetFullPath(file);

                    Marshal.StructureToPtr(fileDescriptor, (IntPtr)ptr, false);
                    ptr += size;
                }
                  
            }

            protected override bool ReleaseHandle()
            {
                if (IsInvalid)
                    return true;

                Type type = typeof(Mapi.MapiFileDescriptor);
                int size = Marshal.SizeOf(type);

                // Get the ptr to the files
                int ptr = (int)handle;
                // Release each file
                for (int i = 0; i < _count; i++)
                {
                    Marshal.DestroyStructure((IntPtr)ptr, type);
                    ptr += size;
                }
                // Release the file
                Marshal.FreeHGlobal(handle);

                SetHandle(IntPtr.Zero);
                _count = 0;

                return true;
            }

            public override bool IsInvalid
            {
                get { return (handle == IntPtr.Zero); }
            }

            public static implicit operator IntPtr(AttachmentCollectionHandle recipientCollectionHandle)
            {
                return recipientCollectionHandle.handle;
            }
        }
    }
}
