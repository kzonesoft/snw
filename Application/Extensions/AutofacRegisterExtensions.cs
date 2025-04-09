using Autofac;
using Kzone.Engine.Bencode.Interfaces;
using Kzone.Engine.Bencode.Repositories;
using Kzone.Engine.Controller.Application.Interfaces;
using Kzone.Engine.Controller.Application.Services;
using Kzone.Engine.Controller.Domain.Exceptions;
using Kzone.Engine.Controller.Domain.Interfaces;
using Kzone.Engine.Controller.Infrastructure.Api;
using System;
using System.Linq;


namespace Kzone.Engine.Controller.Application.Extensions
{
    public static class AutofacRegisterExtensions
    {
        public static void RegisterKzoneEngine(this ContainerBuilder builder)
        {
            var configProvider = AppDomain.CurrentDomain.GetAssemblies()
                                   .SelectMany(a => a.GetTypes())
                                   .Where(t => typeof(IEngineConfigProvider)
                                   .IsAssignableFrom(t) && t.IsClass)
                                   .FirstOrDefault() ?? throw new EngineException("Engine config provider not found!");


            builder.RegisterType(configProvider)
                .As<IEngineConfigProvider>()
                .InstancePerLifetimeScope();

            builder.RegisterType<EngineDataRepository>() //transient
                .As<IEngineDataRepository>()
                .InstancePerDependency();

            builder.RegisterType<EngineSettingRepository>() //transient
                .As<IEngineSettingRepository>()
                .InstancePerDependency();

            builder.RegisterType<TorrentOnDiskService>() //scope
                .As<ITorrentOnDiskService>()
                .InstancePerLifetimeScope();

            builder.RegisterType<EngineSettingsService>() //scope
                .As<IEngineSettingsService>()
                .InstancePerLifetimeScope();

            builder.RegisterType<EngineSetupService>() //scope
                .As<IEngineSetupService>()
                .InstancePerLifetimeScope();

            builder.RegisterType<TorrentManagerService>()  //singleton
                .As<ITorrentManagerService>()
                .SingleInstance();

            builder.RegisterType<TorrentIssueService>() //singleton
              .As<ITorrentIssueService>()
              .SingleInstance();

            builder.RegisterType<BencodeParserService>() //singleton
              .As<IBencodeParserService>()
              .SingleInstance();

            builder.RegisterType<TorrentApi>() //singleton
               .As<ITorrentApi>()
               .SingleInstance();

            builder.RegisterType<EngineController>() //singleton
               .As<IEngineController>()
               .SingleInstance();
        }
    }
}
