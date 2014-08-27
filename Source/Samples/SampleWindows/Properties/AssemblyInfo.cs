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

[assembly: AssemblyTitle("SampleClient")]
[assembly: ComVisible(false)]
[assembly: Guid("01af2994-3a10-4ac4-993b-5e3e58a4c003")]
[assembly: Exceptionless("e3d51ea621464280bbcb79c11fd6483e", ServerUrl = "http://localhost:50000")]

[assembly: AssemblyVersion("2.0.*")]
