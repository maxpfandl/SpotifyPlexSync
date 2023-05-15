using System;
using System.Xml.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace SpotifyPlexSync
{
    public class PlexSearcher
    {
        private XDocument? _tracks;
        private IConfiguration _config;
        private ILogger _logger;
        public PlexSearcher(IConfiguration config, ILogger logger)
        {
            _config = config;
            _logger = logger;
        }

        public async Task Init()
        {
            using (HttpClient client = new HttpClient())
            {
                client.Timeout = TimeSpan.FromHours(1);

                // var searchResult = await client.GetAsync($"{_config?["Plex:Url"]}/library/sections/{_config?["Plex:MusicLibraryKey"]}/all?X-Plex-Container-Size=50&X-Plex-Token={_config?["Plex:Token"]}");
                var searchResult = await client.GetAsync($"{_config?["Plex:Url"]}/library/sections/{_config?["Plex:MusicLibraryKey"]}/all?type=10&X-Plex-Container-Size=50&X-Plex-Token={_config?["Plex:Token"]}");

                if (!searchResult.IsSuccessStatusCode)
                {
                    _logger?.LogError("Error while downloading tracks\n\t" + searchResult.ReasonPhrase);
                    return;
                }

                var result = await searchResult.Content.ReadAsStringAsync();

                // XDocument doc = XDocument.Parse(result);

                File.WriteAllText("tracks.xml", result);

            }
        }

        public string GetKeyForTrack(string artist, string album, string title)
        {
            if (_tracks == null)
                throw new ApplicationException("PlexSearcher not initialized");

            return "";
        }
    }
}