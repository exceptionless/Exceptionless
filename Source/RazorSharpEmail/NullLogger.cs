using System;

namespace RazorSharpEmail
{
    public class NullLogger : ILogger
    {
        static readonly Lazy<ILogger> LoggerInstance = new Lazy<ILogger>(() => new NullLogger());
        public static ILogger Instance { get { return LoggerInstance.Value; } }

        private NullLogger()
        {
        }

        public void Info(Action message)
        {
            
        }
    }
}