using System;
using System.Text;
using MailKit;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Insulation.Mail {
    public class ExtensionsProtocolLogger : IProtocolLogger {
        private const string CLIENT_PREFIX = "Client: ";
        private const string SERVER_PREFIX = "Server: ";

        private readonly ILogger _logger;

        public ExtensionsProtocolLogger(ILogger logger) {
            _logger = logger;
        }

        public void LogConnect(Uri uri) {
            _logger.LogTrace("Connected to {URI}", uri);
        }

        public void LogClient(byte[] buffer, int offset, int count) {
            LogMessage(CLIENT_PREFIX, buffer, offset, count);
        }

        public void LogServer(byte[] buffer, int offset, int count) {
            LogMessage(SERVER_PREFIX, buffer, offset, count);
        }

        private void LogMessage(string prefix, byte[] buffer, int offset, int count) {
            if (!_logger.IsEnabled(LogLevel.Trace))
                return;
            
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));

            if (offset < 0 || offset > buffer.Length)
                throw new ArgumentOutOfRangeException(nameof(offset));

            if (count < 0 || count > (buffer.Length - offset))
                throw new ArgumentOutOfRangeException(nameof(count));
            
            int endIndex = offset + count;
            int index = offset;

            while (index < endIndex) {
                int start = index;
                while (index < endIndex && buffer[index] != (byte)'\n') {
                    index++;
                }

                if (index < endIndex && buffer[index] == (byte)'\n') {
                    index++;
                }

                _logger.LogTrace(prefix + Encoding.Default.GetString(buffer, start, index - start).Trim());
            }
        }
        
        public void Dispose() { }
    }
}
