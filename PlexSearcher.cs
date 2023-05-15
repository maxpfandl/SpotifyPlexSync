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
            // using (HttpClient client = new HttpClient())
            // {
            //     client.Timeout = TimeSpan.FromHours(1);

            //     // var searchResult = await client.GetAsync($"{_config?["Plex:Url"]}/library/sections/{_config?["Plex:MusicLibraryKey"]}/all?X-Plex-Container-Size=50&X-Plex-Token={_config?["Plex:Token"]}");
            //     var searchResult = await client.GetAsync($"{_config?["Plex:Url"]}/library/sections/{_config?["Plex:MusicLibraryKey"]}/all?type=10&X-Plex-Container-Size=50&X-Plex-Token={_config?["Plex:Token"]}");

            //     if (!searchResult.IsSuccessStatusCode)
            //     {
            //         _logger?.LogError("Error while downloading tracks\n\t" + searchResult.ReasonPhrase);
            //         return;
            //     }

            //     var result = await searchResult.Content.ReadAsStringAsync();

            //     // XDocument doc = XDocument.Parse(result);

            //     // File.WriteAllText("tracks.xml", result);

            // }
            _tracks = XDocument.Parse(File.ReadAllText("tracks.xml"));

        }

        public string? GetKeyForTrack(string artist, string album, string title)
        {
            if (_tracks == null)
                throw new ApplicationException("PlexSearcher not initialized");

            var track = from c in _tracks.Descendants("Track")
                        where
                c?.Attribute("title")?.Value.ToLower() == title.ToLower() &&
                c?.Attribute("parentTitle")?.Value.ToLower() == album.ToLower() &&
                c?.Attribute("grandparentTitle")?.Value.ToLower() == artist.ToLower()
                        select c;


            Console.WriteLine(track?.FirstOrDefault()?.Attribute("title")?.Value);
            Console.WriteLine(track?.FirstOrDefault()?.Attribute("parentTitle")?.Value);
            Console.WriteLine(track?.FirstOrDefault()?.Attribute("grandparentTitle")?.Value);


            return track?.FirstOrDefault()?.Attribute("ratingKey")?.Value;
        }
    }
}