#region Copyright 2014 Exceptionless

// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// 
//     http://www.apache.org/licenses/LICENSE-2.0

#endregion

using System;
using System.Reflection;
using System.Runtime.CompilerServices;

[assembly: CLSCompliant(true)]
[assembly: AssemblyTitle("Exceptionless.Extras")]
[assembly: AssemblyDescription("Exceptionless.Extras")]

#if SIGNED
[assembly: InternalsVisibleTo("Client.Tests, PublicKey=0024000004800000940000000602000000240000525341310004000001000100a9357232b9bcad78fd310297fdb41bf42816ee2ca9ccdace999889de2badb6f06df2de1d9f2c8cb17b21f5311f11d6bb328d55e0dd9fe8adc5e2dc4610028c1bdacb3355d2e239b81d0bb0ac83e615fc641f8a3ec49e4fad8e305994953d448ef7b38e8c256601e54af19c035b562e3e5e5461c2a93b8dd11936e451b05034a2")]
#else
[assembly: InternalsVisibleTo("Client.Tests")]
#endif