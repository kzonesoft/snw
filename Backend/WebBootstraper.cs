using Autofac;
using Nancy.Bootstrapper;
using Nancy.Bootstrappers.Autofac;


namespace KzoneSyncService.Presentation.Web.Backend
{
    public class WebBootstraper : AutofacNancyBootstrapper
    {
        private readonly IContainer _container;

        public WebBootstraper(IContainer container)
        {
            _container = container;
        }

        protected override void ApplicationStartup(ILifetimeScope container, IPipelines pipelines)
        {
            base.ApplicationStartup(container, pipelines);

            // Đăng ký JwtMiddleware
            JwtMiddleware.Register(pipelines);
        }

        protected override void ConfigureApplicationContainer(ILifetimeScope existingContainer)
        {
            base.ConfigureApplicationContainer(existingContainer);

        }

        protected override ILifetimeScope GetApplicationContainer()
        {
            return _container;
        }

    }
}
