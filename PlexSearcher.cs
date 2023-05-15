using System;
using System.Xml.Linq;
using Microsoft.Extensions.Configuration;

namespace SpotifyPlexSync
{
    public class PlexSearcher
    {
        private XDocument? _tracks;
        private IConfiguration _config;
        public PlexSearcher(IConfiguration config)
        {
            _config = config;
        }

        public async Task Init()
        {
            using (HttpClient client = new HttpClient())
            {
                client.Timeout = TimeSpan.FromHours(1);

                await client.GetAsync($"");

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