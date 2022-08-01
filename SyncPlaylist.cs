using System.Text.RegularExpressions;
using System.Web;
using System.Xml.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SpotifyAPI.Web;

namespace SpotifyPlexSync
{
    public class SyncPlaylist
    {
        private IConfiguration? _config;

        private static List<SyncPlaylistTrack> _cache = new List<SyncPlaylistTrack>();

        private ILogger? _logger;
        private SyncPlaylist()
        {
            Tracks = new List<SyncPlaylistTrack>();
        }
        public SyncPlaylist(IConfiguration config, ILogger logger)
        {
            Tracks = new List<SyncPlaylistTrack>();
            _config = config;
            _logger = logger;
        }
        public string? Name { get; private set; }
        public string? Description { get; private set; }

        public string? PosterUrl { get; private set; }
        public string? PlexId { get; set; }
        public List<SyncPlaylistTrack> Tracks { get; }

        public bool HasFoundTracks
        {
            get
            {
                return Tracks.Exists(p =>
                {
                    return p.PTrackKey != null;
                });
            }
        }

        public async Task Initialize(FullPlaylist spPlaylist, HttpClient client, SpotifyClient spotify)
        {
            Name = _config?["Prefix"] + spPlaylist.Name;
            if (spPlaylist.Images?.Count > 0)
                PosterUrl = spPlaylist.Images?[0].Url;

            Description = spPlaylist.Description;

            await foreach (var track in spotify.Paginate(spPlaylist.Tracks!))
            {
                var ft = track.Track as FullTrack;
                if (ft != null)
                    Tracks.Add(await SearchSpotifyTracksInPlex(client, ft));
            }

            // foreach (var track in spPlaylist.Tracks?.Items!)
            // {
            //     var ft = track.Track as FullTrack;
            //     if (ft != null)
            //         Tracks.Add(await SearchSpotifyTracksInPlex(client, ft));
            // }
        }

        public string GetReport()
        {

            int found = 0;
            Tracks.ForEach(p =>
            {
                if (p.PTrackKey != null)
                    found++;
            });

            return $"Playlist: {Name} - found {found}/{Tracks.Count}";

        }

        private async Task<SyncPlaylistTrack> SearchSpotifyTracksInPlex(HttpClient client, FullTrack spotifyTrack)
        {
            SyncPlaylistTrack trackresult = new SyncPlaylistTrack();
            trackresult.SpTrack = spotifyTrack;
            var ft = spotifyTrack;
            if (ft != null)
            {

                foreach (var cache in _cache)
                {
                    if (cache.PTrackKey != null && cache?.SpTrack?.Id == ft.Id)
                       return cache;
                }

                var searchTerm = Regex.Replace(ft.Name, @"\(.*?\)", "").Trim(); // remove all in brackets
                searchTerm = HttpUtility.UrlEncode(searchTerm);
                var searchResult = await client.GetAsync(_config?["Plex:Url"] + $"/hubs/search?query={searchTerm}&limit=100&X-Plex-Token={_config?["Plex:Token"]}");

                if (!searchResult.IsSuccessStatusCode)
                {
                    _logger?.LogInformation("Error while searching " + searchTerm + "\n  " + searchResult.ReasonPhrase);
                }

                var result = await searchResult.Content.ReadAsStringAsync();

                XDocument doc = XDocument.Parse(result);
                var found = false;
                foreach (var hub in doc.Descendants("Hub"))
                {
                    if (hub.Attribute("type")?.Value != "track")
                        continue;
                    foreach (var pl in hub.Descendants("Track"))
                    {
                        var key = pl.Attribute("ratingKey")?.Value;
                        var plexTitle = pl.Attribute("title")?.Value;
                        var plexArtist = pl.Attribute("grandparentTitle")?.Value;
                        var plexAlbum = pl.Attribute("parentTitle")?.Value;


                        if (Compare(ft, plexTitle!, plexArtist!))
                        {
                            trackresult.PTrackKey = key;
                            if (!_cache.Contains(trackresult))
                                _cache.Add(trackresult);
                            _logger?.LogInformation("Track found on Plex: \n  Spotify: " + ft.Artists[0].Name + " - " + ft.Album.Name + " - " + ft.Name + "\n  Plex:    " + plexArtist + " - " + plexAlbum + " - " + plexTitle);
                            found = true;
                            break;
                        }
                    }


                }
                if (!found)
                {

                    var text = ft.Artists[0].Name + " - " + ft.Album.Name + " - " + ft.Name + "||" + searchTerm;
                    _logger?.LogWarning("Track not found on Plex: " + text);
                    if (_config?["LogUnmatched"].ToLower() == "true")
                    {
                        File.AppendAllLines($"sptfplexsync_unmatched_{DateTime.Now.ToString("yyyy-MM-dd")}.log", new List<string>() { text });
                    }
                }

            }
            return trackresult;
        }

        private bool Compare(FullTrack track, string plexTitle, string plexArtist)
        {
            var pattern = @"[^0-9a-zA-Z:,]+";
            var spTitle = Regex.Replace(track.Name, pattern, "").ToLower();
            var spArtist = Regex.Replace(track.Artists[0].Name, pattern, "").ToLower();
            var plexTitleNorm = Regex.Replace(plexTitle, pattern, "").ToLower();
            var plexArtistNorm = Regex.Replace(plexArtist, pattern, "").ToLower();

            if (spTitle == plexTitleNorm && spArtist == plexArtistNorm)
                return true;

            if (spTitle.Contains(plexTitleNorm) && spArtist.Contains(plexArtistNorm) && !plexTitle.Contains("(live"))
                return true;

            if (plexTitleNorm.Contains(spTitle) && plexArtistNorm.Contains(spArtist) && !plexTitle.Contains("(live"))
                return true;

            return false;
        }

    }

    public class SyncPlaylistTrack
    {
        public FullTrack? SpTrack { get; set; }
        public string? PTrackKey { get; set; }
    }


}