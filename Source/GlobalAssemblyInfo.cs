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

[assembly: AssemblyProduct("Exceptionless")]
[assembly: AssemblyCompany("Exceptionless")]
[assembly: AssemblyTrademark("Exceptionless")]
[assembly: AssemblyCopyright("Copyright (c) 2014 Exceptionless.  All rights reserved.")]
#if DEBUG

[assembly: AssemblyConfiguration("Debug")]
#else
[assembly: AssemblyConfiguration("Release")]
#endif

[assembly: AssemblyVersion("2.0.0")]
[assembly: AssemblyFileVersion("2.0.0")]
[assembly: AssemblyInformationalVersion("2.0.0")]

internal sealed partial class ThisAssembly {
    internal const string AssemblyCompany = "Exceptionless";

    internal const string AssemblyProduct = "Exceptionless";

    internal const string AssemblyTrademark = "Exceptionless";

    internal const string AssemblyCopyright = "Copyright (c) 2014 Exceptionless.  All rights reserved.";

    internal const string AssemblyConfiguration = "Release";

    internal const string AssemblyVersion = "2.0.0";

    internal const string AssemblyFileVersion = "2.0.0";

    internal const string AssemblyInformationalVersion = "2.0.0";

    private ThisAssembly() {}
}
