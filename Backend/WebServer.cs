using Autofac;
using KzoneSyncService.Application.Interfaces.Services;
using Nancy.Hosting.Self;
using System;

namespace KzoneSyncService.Presentation.Web.Backend
{
    public class WebServer
    {
        private readonly NancyHost _host;
        public WebServer(IContainer container)
        {
            using var scope = container.BeginLifetimeScope();

            var serverSettings = scope.Resolve<IServerSideSettingService>();
            var svcSetting = serverSettings.GetServiceRuntimeConfig();

            var bootstrapper = new WebBootstraper(container);
            var config = new HostConfiguration { UrlReservations = { CreateAutomatically = true } };
            var uri = new Uri($"http://localhost:{svcSetting.WebApiPort}");
            _host = new NancyHost(bootstrapper, config, uri);

        }

        public void Start()
        {
            _host.Start();
        }

        public void Stop()
        {
            _host.Stop();
        }
        public void Dispose()
        {
            _host.Dispose();
        }


    }
}
