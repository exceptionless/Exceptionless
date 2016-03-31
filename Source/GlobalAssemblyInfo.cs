using System;
using System.Reflection;

[assembly: AssemblyProduct("Exceptionless")]
[assembly: AssemblyCompany("Exceptionless")]
[assembly: AssemblyTrademark("Exceptionless")]
[assembly: AssemblyCopyright("Copyright (c) 2016 Exceptionless.  All rights reserved.")]

#if DEBUG
[assembly: AssemblyConfiguration("Debug")]
#else
[assembly: AssemblyConfiguration("Release")]
#endif

[assembly: AssemblyVersion("3.3.0")]
[assembly: AssemblyFileVersion("3.3.0")]
[assembly: AssemblyInformationalVersion("3.3.0")]
