using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using Xunit.Abstractions;

namespace Exceptionless.Api.Tests.Utility {
    public class TestOutputWriter : TextWriter {
        private readonly ITestOutputHelper _output;

        public TestOutputWriter(ITestOutputHelper output) {
            _output = output;
        }

        public override Encoding Encoding => Encoding.UTF8;

        public override void WriteLine(string value) {
            try {
                _output.WriteLine(value);
            } catch (InvalidOperationException) { } catch (Exception ex) {
                Trace.WriteLine(ex);
            }
        }

        public override void WriteLine() {
            WriteLine(String.Empty);
        }
    }
}