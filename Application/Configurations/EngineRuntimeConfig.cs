namespace Kzone.Engine.Controller.Application.Configurations
{
    public class EngineRuntimeConfig
    {
        public int UploadLimited { get; set; }
        public int DownloadLimited { get; set; }
        public int DownloadActiveLimit { get; set; }
        public int DiskCache { get; set; }
        public int EngineListenPort { get; set; }
    }
}
