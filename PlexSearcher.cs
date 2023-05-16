using System;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace SpotifyPlexSync
{
    public class PlexSearcher
    {
        private static XDocument? _tracks;
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
                _logger.LogInformation("Initializing PlexSearcher");

                // var searchResult = await client.GetAsync($"{_config?["Plex:Url"]}/library/sections/{_config?["Plex:MusicLibraryKey"]}/all?X-Plex-Container-Size=50&X-Plex-Token={_config?["Plex:Token"]}");
                var searchResult = await client.GetAsync($"{_config?["Plex:Url"]}/library/sections/{_config?["Plex:MusicLibraryKey"]}/all?type=10&X-Plex-Token={_config?["Plex:Token"]}");

                if (!searchResult.IsSuccessStatusCode)
                {
                    _logger?.LogError("Error while downloading tracks\n\t" + searchResult.ReasonPhrase);
                    return;
                }

                if (searchResult.Content != null)
                {
                    var result = await searchResult.Content.ReadAsStringAsync();

                    // File.WriteAllText("tracks.xml", result);
                    _tracks = XDocument.Parse(result);
                }



            }
            // _tracks = XDocument.Parse(File.ReadAllText("tracks.xml"));

        }


        public SyncPlaylistTrack? GetKeyForTrack(string artist, string album, string title)
        {
            if (_tracks == null)
                throw new ApplicationException("PlexSearcher not initialized");

            var track = from c in _tracks.Descendants("Track")
                        where
                c?.Attribute("title")?.Value.Clean() == title.Clean() &&
                c?.Attribute("parentTitle")?.Value.Clean() == album.Clean() &&
                c?.Attribute("grandparentTitle")?.Value.Clean() == artist.Clean()
                        select c;

            if (track.FirstOrDefault() == null)
            {
                track = from c in _tracks.Descendants("Track")
                        where
                c?.Attribute("title")?.Value.Clean() == title.Clean() &&
                c?.Attribute("grandparentTitle")?.Value.Clean() == artist.Clean()
                        select c;
            }

            if (track.FirstOrDefault() == null)
            {
                track = from c in _tracks.Descendants("Track")
                        where
                c.Attribute("title")!.Value.Clean().Contains(title.Clean()) &&
                c?.Attribute("grandparentTitle")?.Value.Clean() == artist.Clean()
                        select c;
            }

            if (track.FirstOrDefault() == null)
            {
                track = from c in _tracks.Descendants("Track")
                        where
                title.Clean().Contains(c.Attribute("title")!.Value.Clean()) &&
                c?.Attribute("grandparentTitle")?.Value.Clean() == artist.Clean()
                        select c;
            }

            if (track.FirstOrDefault() == null)
            {
                track = from c in _tracks.Descendants("Track")
                        where
                title.Clean().Contains(c.Attribute("title")!.Value.Clean()) &&
                artist.Clean().Contains(c.Attribute("grandparentTitle")!.Value.Clean())
                        select c;
            }

            if (track.FirstOrDefault() == null)
            {
                track = from c in _tracks.Descendants("Track")
                        where
                c.Attribute("title")!.Value.Clean().Contains(title.Clean()) &&
                c.Attribute("grandparentTitle")!.Value.Clean().Contains(artist.Clean())
                        select c;
            }

            // Console.WriteLine(track?.FirstOrDefault()?.Attribute("title")?.Value);
            // Console.WriteLine(track?.FirstOrDefault()?.Attribute("parentTitle")?.Value);
            // Console.WriteLine(track?.FirstOrDefault()?.Attribute("grandparentTitle")?.Value);
            // Console.WriteLine();


            SyncPlaylistTrack result = new SyncPlaylistTrack();
            result.PTrack = new PlexTrack();
            result.PTrackKey = track?.FirstOrDefault()?.Attribute("ratingKey")?.Value;
            result.PTrack.Artist = track?.FirstOrDefault()?.Attribute("grandparentTitle")?.Value;
            result.PTrack.Title = track?.FirstOrDefault()?.Attribute("title")?.Value;
            result.PTrack.Album = track?.FirstOrDefault()?.Attribute("parentTitle")?.Value;


            return result;
        }

    }

    public static class StringExtension
    {
        public static string Clean(this string mystring)
        {
            var result = mystring.ToLower();
            result = Regex.Replace(result, "[^0-9a-zA-Z]+", "");
            return result;
        }
    }

}