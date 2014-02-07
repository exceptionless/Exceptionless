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

namespace CodeSmith.Core.CommandLine
{
	/// <summary>
	/// Indicates that this argument is the default argument.
	/// '/' or '-' prefix only the argument value is specified.
	/// The ShortName property should not be set for DefaultArgumentAttribute
	/// instances. The LongName property is used for usage text only and
	/// does not affect the usage of the argument.
	/// </summary>
	[AttributeUsage(AttributeTargets.Field)]
	public class DefaultArgumentAttribute : ArgumentAttribute
	{
		/// <summary>
		/// Indicates that this argument is the default argument.
		/// </summary>
		/// <param name="type"> Specifies the error checking to be done on the argument. </param>
		public DefaultArgumentAttribute(ArgumentType type)
			: base (type)
		{
		}
	}
}