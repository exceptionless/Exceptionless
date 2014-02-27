using Nancy;
using Nancy.TinyIoc;
using Nancy.Bootstrapper;
using Exceptionless;

namespace Exceptionless.SampleNancy
{
    public class ExceptionlessBootstrapper : DefaultNancyBootstrapper
    {
        protected override void ApplicationStartup(TinyIoCContainer container, IPipelines pipelines)
        {
            base.ApplicationStartup(container, pipelines);
            ExceptionlessClient.Current.RegisterNancy(pipelines);
        }
    }
}