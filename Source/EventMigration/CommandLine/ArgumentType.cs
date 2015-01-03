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

namespace Exceptionless.EventMigration.CommandLine
{
	/// <summary>
	/// Used to control parsing of command line arguments.
	/// </summary>
	[Flags]    
	public enum ArgumentType
	{
		/// <summary>
		/// Indicates that this field is required. An error will be displayed
		/// if it is not present when parsing arguments.
		/// </summary>
		Required    = 0x01,
		/// <summary>
		/// Only valid in conjunction with Multiple.
		/// Duplicate values will result in an error.
		/// </summary>
		Unique      = 0x02,
		/// <summary>
		/// Indicates that the argument may be specified more than once.
		/// Only valid if the argument is a collection
		/// </summary>
		Multiple    = 0x04,

		/// <summary>
		/// The default type for non-collection arguments.
		/// The argument is not required, but an error will be reported if it is specified more than once.
		/// </summary>
		AtMostOnce  = 0x00,
        
		/// <summary>
		/// For non-collection arguments, when the argument is specified more than
		/// once no error is reported and the value of the argument is the last
		/// value which occurs in the argument list.
		/// </summary>
		LastOccurenceWins = Multiple,

		/// <summary>
		/// The default type for collection arguments.
		/// The argument is permitted to occur multiple times, but duplicate 
		/// values will cause an error to be reported.
		/// </summary>
		MultipleUnique  = Multiple | Unique,
	}
}