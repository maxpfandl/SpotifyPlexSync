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

        public string? Author { get; private set; }

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
            Author = spPlaylist.Owner?.DisplayName;
            if (_config.GetValue<bool>("AddAuthorToTitle") && !string.IsNullOrEmpty(Author))
            {
                Name = Name + " by " + Author;
            }

            if (spPlaylist.Images?.Count > 0)
                PosterUrl = spPlaylist.Images?[0].Url;

            Description = spPlaylist.Description;

            List<PlexTrack> existingPlaylist = new List<PlexTrack>();

            try
            {
                var pPlaylistKey = await Program.GetPlaylist(Name, client);
                if (pPlaylistKey != null)
                {
                    var plexList = await client.GetAsync($"{_config?["Plex:Url"]}/playlists/{pPlaylistKey}/items?X-Plex-Token={_config?["Plex:Token"]}");

                    XDocument doc = XDocument.Parse(await plexList.Content.ReadAsStringAsync());

                    foreach (var playlist in doc.Descendants("Track"))
                    {
                        PlexTrack track = new PlexTrack();
                        track.Title = playlist.Attribute("title")?.Value;
                        track.Album = playlist.Attribute("parentTitle")?.Value;
                        track.Artist = playlist.Attribute("grandparentTitle")?.Value;
                        track.Key = playlist.Attribute("ratingKey")?.Value;
                        existingPlaylist.Add(track);
                    }
                }

            }
            catch (Exception ex)
            {
                _logger?.LogError("Playlistsearch in Plex not working", ex);
            }

            try
            {
                List<FullTrack> items = new List<FullTrack>();
                await foreach (var track in spotify.Paginate(spPlaylist.Tracks!))
                {
                    FullTrack? ft = track.Track as FullTrack;
                    if (ft != null)
                        items.Add(ft);
                }

                foreach (var ft in items)
                {
                    if (ft != null)
                    {
                        try
                        {
                            Tracks.Add(await SearchSpotifyTracksInPlex(client, ft, existingPlaylist));
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogError("Track not found with exception: " + ex.Message);
                        }
                    }

                }
            }
            catch (Exception ex)
            {
                _logger?.LogError("Playlist init failed: " + ex.Message);
            }

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

        private async Task<SyncPlaylistTrack> SearchSpotifyTracksInPlex(HttpClient client, FullTrack spotifyTrack, List<PlexTrack> existingPlaylist)
        {
            SyncPlaylistTrack trackresult = new SyncPlaylistTrack();
            trackresult.SpTrack = spotifyTrack;
            var ft = spotifyTrack;
            if (ft != null)
            {

                foreach (var cache in _cache)
                {
                    if (cache.PTrackKey != null && cache?.SpTrack?.Id == ft.Id)
                    {
                        _logger?.LogInformation("Track found in Cache: \n  Spotify: " + ft.Artists[0].Name + " - " + ft.Album.Name + " - " + ft.Name + "\n  Plex:    " + cache.PTrackKey);
                        return cache;
                    }
                }


                foreach (var existingTrack in existingPlaylist)
                {
                    if (CompareStrict(ft, existingTrack.Title!, existingTrack.Artist!))
                    {
                        trackresult.PTrackKey = existingTrack.Key;
                        trackresult.PTrack = existingTrack;
                        if (!_cache.Contains(trackresult))
                            _cache.Add(trackresult);
                        _logger?.LogInformation("Track found in existing Playlist: \n  Spotify: " + ft.Artists[0].Name + " - " + ft.Album.Name + " - " + ft.Name + "\n  Plex:    " + existingTrack.Artist + " - " + existingTrack.Album + " - " + existingTrack.Title);
                        return trackresult;
                    }
                }

                var searchTerm = Regex.Replace(ft.Name, @"\(.*?\)", "").Trim(); // remove all in () brackets
                searchTerm = Regex.Replace(searchTerm, @"\[.*?\]", "").Trim(); // remove all in [] brackets
                searchTerm = ft.Artists[0].Name + " " + searchTerm;
                searchTerm = HttpUtility.UrlEncode(searchTerm);
                var searchResult = await client.GetAsync(_config?["Plex:Url"] + $"/hubs/search?query={searchTerm}&limit=100&X-Plex-Token={_config?["Plex:Token"]}");

                if (!searchResult.IsSuccessStatusCode)
                {
                    _logger?.LogError("Error while searching " + searchTerm + "\n  " + searchResult.ReasonPhrase);
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


                        if (CompareStrict(ft, plexTitle!, plexArtist!))
                        {
                            trackresult.PTrackKey = key;
                            if (!_cache.Contains(trackresult))
                                _cache.Add(trackresult);
                            _logger?.LogInformation("Track found strict on Plex: \n  Spotify: " + ft.Artists[0].Name + " - " + ft.Album.Name + " - " + ft.Name + "\n  Plex:    " + plexArtist + " - " + plexAlbum + " - " + plexTitle);
                            found = true;
                            break;
                        }
                    }

                    if (!found)
                    {
                        foreach (var pl in hub.Descendants("Track"))
                        {
                            var key = pl.Attribute("ratingKey")?.Value;
                            var plexTitle = pl.Attribute("title")?.Value;
                            var plexArtist = pl.Attribute("grandparentTitle")?.Value;
                            var plexAlbum = pl.Attribute("parentTitle")?.Value;


                            if (CompareFuzzy(ft, plexTitle!, plexArtist!))
                            {
                                trackresult.PTrackKey = key;
                                if (!_cache.Contains(trackresult))
                                    _cache.Add(trackresult);
                                _logger?.LogInformation("Track found fuzzy on Plex: \n  Spotify: " + ft.Artists[0].Name + " - " + ft.Album.Name + " - " + ft.Name + "\n  Plex:    " + plexArtist + " - " + plexAlbum + " - " + plexTitle);
                                found = true;
                                break;
                            }
                        }
                    }

                }
                if (!found)
                {

                    var text = ft.Artists[0].Name + " - " + ft.Album.Name + " - " + ft.Name + "||" + searchTerm;
                    _logger?.LogWarning("Track not found on Plex: " + text);
                    if (_config?.GetValue<bool>("LogUnmatched") ?? false)
                    {
                        File.AppendAllLines($"sptfplexsync_unmatched_{DateTime.Now.ToString("yyyy-MM-dd")}.log", new List<string>() { text });
                    }
                }

            }
            return trackresult;
        }

        private bool CompareStrict(FullTrack track, string plexTitle, string plexArtist)
        {
            var pattern = @"[^0-9a-zA-Z:,]+";
            var spTitle = Regex.Replace(track.Name, pattern, "").ToLower();
            var spArtist = Regex.Replace(track.Artists[0].Name, pattern, "").ToLower();
            var plexTitleNorm = Regex.Replace(plexTitle, pattern, "").ToLower();
            var plexArtistNorm = Regex.Replace(plexArtist, pattern, "").ToLower();

            if (spTitle == plexTitleNorm && spArtist == plexArtistNorm)
                return true;

            return false;
        }

        private bool CompareFuzzy(FullTrack track, string plexTitle, string plexArtist)
        {
            var pattern = @"[^0-9a-zA-Z:,]+";
            var spTitle = Regex.Replace(track.Name, pattern, "").ToLower();
            var spArtist = Regex.Replace(track.Artists[0].Name, pattern, "").ToLower();
            var plexTitleNorm = Regex.Replace(plexTitle, pattern, "").ToLower();
            var plexArtistNorm = Regex.Replace(plexArtist, pattern, "").ToLower();

            if (spTitle == plexTitleNorm && spArtist == plexArtistNorm)
                return true;

            if (spTitle.Contains(plexTitleNorm) && spArtist.Contains(plexArtistNorm))
                return true;

            if (plexTitleNorm.Contains(spTitle) && plexArtistNorm.Contains(spArtist))
                return true;

            return false;
        }

    }

    public class SyncPlaylistTrack
    {
        public FullTrack? SpTrack { get; set; }
        public string? PTrackKey { get; set; }
        public PlexTrack? PTrack { get; set; }
    }

    public class PlexTrack
    {
        public string? Key { get; set; }
        public string? Artist { get; set; }
        public string? Album { get; set; }
        public string? Title { get; set; }
    }
}