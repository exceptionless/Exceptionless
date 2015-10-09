using System;
using System.Reflection;

[assembly: AssemblyProduct("Exceptionless")]
[assembly: AssemblyCompany("Exceptionless")]
[assembly: AssemblyTrademark("Exceptionless")]
[assembly: AssemblyCopyright("Copyright (c) 2015 Exceptionless.  All rights reserved.")]

#if DEBUG
[assembly: AssemblyConfiguration("Debug")]
#else
[assembly: AssemblyConfiguration("Release")]
#endif

[assembly: AssemblyVersion("3.1.0")]
[assembly: AssemblyFileVersion("3.1.0")]
[assembly: AssemblyInformationalVersion("3.1.0")]
