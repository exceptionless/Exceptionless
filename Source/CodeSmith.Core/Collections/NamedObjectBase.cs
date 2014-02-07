//------------------------------------------------------------------------------
//
// Copyright (c) 2002-2014 CodeSmith Tools, LLC.  All rights reserved.
// 
// The terms of use for this software are contained in the file
// named sourcelicense.txt, which can be found in the root of this distribution.
// By using this software in any fashion, you are agreeing to be bound by the
// terms of this license.
// 
// You must not remove this notice, or any other, from this software.
//
//------------------------------------------------------------------------------

using System;
using System.ComponentModel;

namespace CodeSmith.Core.Collections
{
    /// <summary>
    /// Provides a base for objects with names to derive from.
    /// </summary>
    [Serializable]
    public abstract class NamedObjectBase : INamedObject
    {
        protected string _name = String.Empty;

        public NamedObjectBase()
        {
        }

        public NamedObjectBase(string name)
        {
            _name = name;
        }

        /// <summary>
        /// The name of the object.
        /// </summary>
        [Description("The Name of the object.")]
        public virtual string Name
        {
            get { return _name; }
        }

        /// <summary>
        /// Returns the name of the table.
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return Name;
        }
    }

    public interface INamedObject
    {
        /// <summary>
        /// The name of the object.
        /// </summary>
        [Description("The Name of the schema object.")]
        string Name { get; }
    }

}
