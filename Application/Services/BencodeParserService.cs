using Kzone.Engine.Bencode.Parsing;
using Kzone.Engine.Bencode.Torrents;
using Kzone.Engine.Controller.Application.Interfaces;
using System;
using System.IO;

namespace Kzone.Engine.Controller.Application.Services
{
    public class BencodeParserService : IBencodeParserService
    {
        public BTorrent ParseBTorrentFromBytes(byte[] torrentBytes)
        {
            ValidateTorrentBytes(torrentBytes);

            using (var memoryStream = new MemoryStream(torrentBytes))
            {
                return ParseBTorrentFromStream(memoryStream);
            }
        }

        public BTorrent ParseBTorrentFromStream(Stream torrentStream)
        {
            ValidateTorrentStream(torrentStream);
            return new BencodeParser().Parse<BTorrent>(torrentStream);
        }

        private void ValidateTorrentBytes(byte[] torrentBytes)
        {
            if (torrentBytes == null || torrentBytes.Length == 0)
            {
                throw new ArgumentException("Torrent không hợp lệ: Dữ liệu trống");
            }
        }

        private void ValidateTorrentStream(Stream torrentStream)
        {
            if (torrentStream == null || torrentStream.Length == 0)
            {
                throw new ArgumentException("Torrent không hợp lệ: Stream dữ liệu trống");
            }
        }
    }
}
