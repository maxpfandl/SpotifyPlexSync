using Microsoft.Extensions.Configuration;
using SpotifyAPI.Web;
using System.Xml.Linq;
using System.Text.RegularExpressions;
using System.Web;
using Microsoft.Extensions.Logging;

namespace SpotifyPlexSync
{
    internal class Program
    {
        private static ILogger? _logger;
        private static IConfiguration? _config;


        static async Task Main(string[] args)
        {



            var builder = new ConfigurationBuilder()
             .AddJsonFile($"appsettings.json", true, true)
             .AddJsonFile($"appsettings.my.json", true, true);
            _config = builder.Build();

            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder
                    .AddConfiguration(_config)
                    .AddConsole()
                    .AddFile("app_{0:yyyy}-{0:MM}-{0:dd}.log", fileLoggerOpts =>
                    {
                        fileLoggerOpts.FormatLogFileName = fName =>
                        {
                            return String.Format(fName, DateTime.UtcNow);
                        };
                    });
            });

            _logger = loggerFactory.CreateLogger<Program>();

            try
            {
                var spotifyConfig = SpotifyClientConfig.CreateDefault();
                var request = new ClientCredentialsRequest(_config?["Spotify:ClientID"]!, _config?["Spotify:ClientSecret"]!);
                var response = await new OAuthClient(spotifyConfig).RequestToken(request);
                var spotify = new SpotifyClient(spotifyConfig.WithToken(response.AccessToken));
                var playlists = _config?.GetSection("Sync").Get<List<string>>();
                foreach (var playlist in playlists!)
                {
                    try
                    {
                        var id = playlist.Split('|')[0];
                        var spotifyPlaylist = await spotify.Playlists.Get(id);

                        _logger.LogInformation("Working on Spotifyplaylist: " + spotifyPlaylist.Name);
                        await CreateOrUpdatePlexPlayList(spotifyPlaylist);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError("CreateUpdate Playlist in Plex failed", ex);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Getting Playlists from Spotify failed", ex);
            }

        }

        private static async Task CreateOrUpdatePlexPlayList(FullPlaylist spotifyPl)
        {
            string title = _config?["Prefix"] + spotifyPl.Name;
            using (HttpClient client = new HttpClient())
            {

                int? tracks = spotifyPl.Tracks?.Items?.Count;
                List<Tuple<string?, FullTrack>> plexIds = new List<Tuple<string?, FullTrack>>();

                foreach (var track in spotifyPl.Tracks?.Items!)
                {
                    plexIds.AddRange(await SearchSpotifyTracksInPlex(client, (FullTrack)track.Track));
                }
                _logger?.LogInformation($"Playlist: {title} - found {plexIds.Count}/{tracks}");


                if (plexIds.Count > 0)
                {
                    Tuple<string?, string?>? playListKeys = await GetOrCreatePlaylist(title, client);

                    if (playListKeys == null)
                        return;


                    _logger?.LogInformation("PlaylistID in Plex: " + playListKeys.Item2);

                    //update description and poster
                    if (spotifyPl?.Images?.Count > 0)
                    {
                        var poster = spotifyPl.Images[0].Url;
                        await client.PostAsync($"{_config?["Plex:Url"]}/library/metadata/{playListKeys.Item1}/posters?url={poster}&X-Plex-Token={_config?["Plex:Token"]}", null);
                    }

                    await client.PutAsync($"{_config?["Plex:Url"]}/playlists/{playListKeys.Item1}?summary={spotifyPl?.Description}&X-Plex-Token={_config?["Plex:Token"]}", null);

                    foreach (var tpl in plexIds)
                    {
                        var ft = tpl.Item2;
                        var key = tpl.Item1;
                        _logger?.LogInformation("Adding to Playlist (" + title + "): " + ft.Artists[0].Name + " - " + ft.Album.Name + " - " + ft.Name);
                        await client.PutAsync($"{_config?["Plex:Url"]}{playListKeys.Item2}?uri=server%3A%2F%2F{_config?["Plex:ServerId"]}%2Fcom.plexapp.plugins.library%2Flibrary%2Fmetadata%2F{key}&X-Plex-Token={_config?["Plex:Token"]}", null);
                    }
                }
                else
                {
                    _logger?.LogError("No Titles found in Plex for Playlist " + title);
                }


            }

        }

        private static async Task<Tuple<string?, string?>?> GetOrCreatePlaylist(string title, HttpClient client)
        {
            Tuple<string?, string?>? playListResult = null;

            _logger?.LogInformation("Search for Playlist in Plex" + title);
            var plexList = await client.GetAsync($"{_config?["Plex:Url"]}/playlists?title={title}&X-Plex-Token={_config?["Plex:Token"]}");

            XDocument doc = XDocument.Parse(await plexList.Content.ReadAsStringAsync());

            // clear playlist
            if (doc.Descendants("Playlist").Count() == 1)
            {
                _logger?.LogInformation("Found Playlist: clearing Items");
                foreach (var pl in doc.Descendants("Playlist"))
                {
                    playListResult = new Tuple<string?, string?>(pl.Attribute("ratingKey")?.Value, pl.Attribute("key")?.Value);
                    await client.DeleteAsync($"{_config?["Plex:Url"]}{playListResult.Item2}?X-Plex-Token={_config?["Plex:Token"]}");
                }
            }
            else if (doc.Descendants("Playlist").Count() == 0)
            {
                _logger?.LogInformation("Playlist not found: creating");
                var result = await client.PostAsync($"{_config?["Plex:Url"]}/playlists?uri=server%3A%2F%{_config?["Plex:ServerId"]}%2Fcom.plexapp.plugins.library%2Flibrary%2Fmetadata%2F1109%2C1200&includeExternalMedia=1&title={title}&smart=0&type=audio&X-Plex-Token={_config?["Plex:Token"]}", null);

                doc = XDocument.Parse(await result.Content.ReadAsStringAsync());
                foreach (var pl in doc.Descendants("Playlist"))
                {
                    playListResult = new Tuple<string?, string?>(pl.Attribute("ratingKey")?.Value, pl.Attribute("key")?.Value);
                }

            }
            else
            {
                _logger?.LogError("PlaylistTitle ambiguous: " + title);
            }
            return playListResult;
        }

        private static async Task<List<Tuple<string?, FullTrack>>> SearchSpotifyTracksInPlex(HttpClient client, FullTrack spotifyTrack)
        {
            var resultList = new List<Tuple<string?, FullTrack>>();
            var ft = spotifyTrack;
            if (ft != null)
            {

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
                            resultList.Add(new Tuple<string?, FullTrack>(key, ft));
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
                        File.AppendAllLines($"unmatched_{DateTime.Now.ToString("yyyy-MM-dd")}.log", new List<string>() { text });
                    }
                }

            }
            return resultList;
        }
        private static bool Compare(FullTrack track, string plexTitle, string plexArtist)
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
}