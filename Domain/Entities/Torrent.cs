namespace Kzone.Engine.Controller.Domain.Entities
{
    public class Torrent
    {
        public string Name { get; set; }
        public string Hash { get; set; }
        public long Size { get; set; }
        public long RemainingSize { get; set; }
        public string Status { get; set; }
        public int Progress { get; set; }
        public double UploadSpeed { get; set; }
        public double DownloadSpeed { get; set; }
    }

}
