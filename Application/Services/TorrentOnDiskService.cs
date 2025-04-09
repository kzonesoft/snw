using Kzone.Engine.Bencode.Core.Elements;
using Kzone.Engine.Bencode.Core.Exceptions;
using Kzone.Engine.Bencode.Interfaces;
using Kzone.Engine.Controller.Application.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Kzone.Engine.Controller.Application.Services
{
    public class TorrentOnDiskService : ITorrentOnDiskService
    {
        private readonly IEngineDataRepository _engineDataRepository;
        public TorrentOnDiskService(IEngineDataRepository engineDataRepository)
        {
            _engineDataRepository = engineDataRepository;
        }

        public int TorrentCount()
        {
            return _engineDataRepository.Count();
        }

        public Dictionary<string, string> TorrentsLocation()
        {
            try
            {
                return _engineDataRepository.GetAll().Values
                    .Cast<BDictionary>()
                    .ToDictionary(
                        element => element.Value[new BString("caption")].ToString(),
                        element => element.Value[new BString("path")].ToString()
                    );
            }
            catch (ArgumentException ex)
            {
                Console.WriteLine($"Error: Duplicate key found - {ex.Message}");

                return _engineDataRepository.GetAll().Values
                    .Cast<BDictionary>()
                    .GroupBy(element => element.Value[new BString("caption")].ToString())
                    .ToDictionary(
                        group => group.Key,
                        group => group.First().Value[new BString("path")].ToString()
                    );
            }
            catch (Exception ex)
            {
                throw new BencodeException($"An unexpected error occurred: {ex.Message}");
            }
        }
    }
}
