using System;

namespace Kzone.Engine.Controller.Application.Configurations
{
    public class EngineLaunchConfig
    {
        public string LicenseId { get; set; }
        public int AppInitTimeout { get; set; }
        public Func<string, string> TokenCallback { get; set; }
    }
}
