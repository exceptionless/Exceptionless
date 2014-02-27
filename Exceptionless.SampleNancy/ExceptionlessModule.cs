using Nancy;
using System;

namespace Exceptionless.SampleNancy
{
    public class ExceptionlessModule : NancyModule
    {
        public ExceptionlessModule()
        {
            Get["/"] = _ => "Hello!";
            Get["/error"] = _ =>
            {
                throw new Exception("Unhandled Exception");
            };
            Get["/custom"] = _ =>
            {
                ExceptionlessClient
                    .Create(new Exception("Handled Exception"))
                        .AddDefaultInformation()
                        .AddRequestInfo(this.Context)
                        .Submit();
                return "ok, handled";
            };
        }
    }
}