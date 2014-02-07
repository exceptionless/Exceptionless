using System;

namespace RazorSharpEmail
{
    public interface ILogger
    {
        void Info(Action message);
    }
}