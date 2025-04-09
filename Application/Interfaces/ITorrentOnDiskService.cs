using System.Collections.Generic;

namespace Kzone.Engine.Controller.Application.Interfaces
{
    public interface ITorrentOnDiskService
    {
        int TorrentCount();
        Dictionary<string, string> TorrentsLocation();
    }
}