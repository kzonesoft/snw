using Kzone.Engine.Bencode.Torrents;
using System.IO;

namespace Kzone.Engine.Controller.Application.Interfaces
{
    public interface IBencodeParserService
    {
        BTorrent ParseBTorrentFromBytes(byte[] torrentBytes);
        BTorrent ParseBTorrentFromStream(Stream torrentStream);
    }
}