#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using System.Reflection;
using System.Runtime.InteropServices;
using Exceptionless.Configuration;

// General Information about an assembly is controlled through the following 
// set of attributes. Change these attribute values to modify the information
// associated with an assembly.

[assembly: AssemblyTitle("Exceptionless.Client.Tests")]
[assembly: ComVisible(false)]
[assembly: Guid("35a87e09-fe5e-4034-9a1f-315368322459")]

[assembly: Exceptionless("http://localhost:40000", "e3d51ea621464280bbcb79c11fd6483e")]
[assembly: ExceptionlessSetting("UserNamespaces", "FromAttribute")]
[assembly: ExceptionlessSetting("AttributeOnly", "Attribute")]
[assembly: ExceptionlessSetting("ConfigAndAttribute", "Attribute")]