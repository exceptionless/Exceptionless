using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Foundatio.Logging;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Exceptionless.Api.Tests.Utility {
    public class CaptureFixture : IDisposable {
        private List<TraceListener> _oldListeners;
        private TextWriter _oldOut;
        private TextWriter _oldError;
        private TextWriter _outputWriter;

        static CaptureFixture() {
#if DEBUG
            Logger.SetMinimumLogLevel(LogLevel.Info);
#else
            Logger.SetMinimumLogLevel(LogLevel.Warn);
#endif

            Logger.RegisterWriter(l => Trace.WriteLine(l.ToString(false, false)));
        }

        public void Capture(ITestOutputHelper output) {
            _outputWriter = new TestOutputWriter(output);
            _oldOut = Console.Out;
            _oldError = Console.Error;
            _oldListeners = new List<TraceListener>();

            try {
                foreach (TraceListener oldListener in Trace.Listeners)
                    _oldListeners.Add(oldListener);

                Trace.Listeners.Clear();
                Trace.Listeners.Add(new AssertTraceListener());
                Trace.Listeners.Add(new TextWriterTraceListener(_outputWriter));

                Console.SetOut(_outputWriter);
                Console.SetError(_outputWriter);
            } catch {}
        }

        public void Dispose() {
            _outputWriter?.Dispose();

            if (_oldOut != null)
                Console.SetOut(_oldOut);

            if (_oldError != null)
                Console.SetError(_oldError);

            Logger.RegisterWriter(l => { });

            try {
                if (_oldListeners != null) {
                    Trace.Listeners.Clear();
                    Trace.Listeners.AddRange(_oldListeners.ToArray());
                }
            } catch (Exception) {}
        }

        private class AssertTraceListener : TraceListener {
            public override void Fail(string message, string detailMessage) {
                throw new TrueException(String.Concat(message, ": ", detailMessage), null);
            }

            public override void Write(string message) {}

            public override void WriteLine(string message) {}
        }
    }
    
    [CollectionDefinition("Capture")]
    public class CaptureCollectionFixture : ICollectionFixture<CaptureFixture> { }

    [Collection("Capture")]
    public abstract class CaptureTests : IDisposable {
        private readonly CaptureFixture _fixture;
        protected readonly ITestOutputHelper _output;
        protected readonly TestOutputWriter _writer;

        protected CaptureTests(CaptureFixture fixture, ITestOutputHelper output) {
            _fixture = fixture;
            _output = output;
            _writer = new TestOutputWriter(_output);

            fixture.Capture(_output);
        }

        public virtual void Dispose() {
            _fixture?.Dispose();
        }
    }
}