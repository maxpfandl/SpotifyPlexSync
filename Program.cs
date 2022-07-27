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
                    .AddFile("sptfplexsync_{0:yyyy}-{0:MM}-{0:dd}.log", fileLoggerOpts =>
                    {
                        fileLoggerOpts.FormatLogFileName = fName =>
                        {
                            return String.Format(fName, DateTime.UtcNow);
                        };
                    });
            });

            _logger = loggerFactory.CreateLogger<Program>();
            List<string> reports = new List<string>();

            try
            {
                var spotifyConfig = SpotifyClientConfig.CreateDefault();
                var request = new ClientCredentialsRequest(_config?["Spotify:ClientID"]!, _config?["Spotify:ClientSecret"]!);
                var response = await new OAuthClient(spotifyConfig).RequestToken(request);
                var spotify = new SpotifyClient(spotifyConfig.WithToken(response.AccessToken));
                var playlists = _config?.GetSection("Sync").Get<List<string>>();


                // single list
                if (args != null && args.Length == 1 && !String.IsNullOrEmpty(args[0]))
                {
                    try
                    {
                        var spotifyPlaylist = await spotify.Playlists.Get(args[0]);

                        _logger.LogInformation("Working on Spotifyplaylist: " + spotifyPlaylist.Name);
                        reports.Add(await CreateOrUpdatePlexPlayList(spotifyPlaylist));
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError("CreateUpdate Playlist in Plex failed", ex);
                    }
                }
                else
                {
                    foreach (var playlist in playlists!)
                    {
                        try
                        {
                            var id = playlist.Split('|')[0];
                            var spotifyPlaylist = await spotify.Playlists.Get(id);

                            _logger.LogInformation("Working on Spotifyplaylist: " + spotifyPlaylist.Name);

                            reports.Add(await CreateOrUpdatePlexPlayList(spotifyPlaylist));
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError("CreateUpdate Playlist in Plex failed", ex);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Getting Playlists from Spotify failed", ex);
            }

            var message = String.Join(Environment.NewLine, reports);

            _logger.LogInformation(message);

        }

        private static async Task<string> CreateOrUpdatePlexPlayList(FullPlaylist spotifyPl)
        {
            string report = "";

            SyncPlaylist playList = new SyncPlaylist(_config!, _logger!);




            using (HttpClient client = new HttpClient())
            {

                await playList.Initialize(spotifyPl, client);

                report = playList.GetReport();
                _logger?.LogInformation(report);


                if (playList.HasFoundTracks)
                {
                    Tuple<string?, string?>? playListKeys = await GetOrCreatePlaylist(playList.Name!, client);

                    if (playListKeys == null)
                        return report;

                    playList.PlexId = playListKeys?.Item1;

                    _logger?.LogInformation("PlaylistID in Plex: " + playList.PlexId);




                    var existingPL = await client.GetAsync($"{_config?["Plex:Url"]}/playlists/{playList.PlexId}/items?X-Plex-Token={_config?["Plex:Token"]}");
                    List<Tuple<string, string>> existingKeys = new List<Tuple<string, string>>();

                    XDocument doc = XDocument.Parse(await existingPL.Content.ReadAsStringAsync());

                    foreach (var pl in doc.Descendants("Track"))
                    {
                        var key = pl.Attribute("ratingKey")?.Value;
                        var playlistKey = pl.Attribute("playlistItemID")?.Value;

                        existingKeys.Add(new Tuple<string, string>(key!, playlistKey!));

                    }

                    List<string> toDelete = new List<string>();
                    List<string> toAdd = new List<string>();

                    foreach (var item in playList.Tracks)
                    {
                        bool found = false;
                        foreach (var exist in existingKeys)
                        {
                            if (exist.Item1 == item.PTrackKey)
                            {
                                found = true;
                                continue;
                            }
                        }
                        if (!found)
                        {
                            if (item.PTrackKey != null)
                            {
                                toAdd.Add(item.PTrackKey);
                                _logger?.LogInformation("Adding to Playlist (" + playList.Name + "): " + item.SpTrack?.Artists[0].Name + " - " + item.SpTrack?.Album.Name + " - " + item.SpTrack?.Name);
                            }
                        }


                    }

                    foreach (var exist in existingKeys)
                    {
                        bool found = false;
                        foreach (var item in playList.Tracks)
                        {
                            if (exist.Item1 == item.PTrackKey)
                            {
                                found = true;
                                continue;
                            }
                        }
                        if (!found)
                        {
                            toDelete.Add(exist.Item2);
                            _logger?.LogInformation("Removing from Playlist (" + playList.Name + "): " + exist.Item1);
                        }
                    }

                    foreach (var del in toDelete)
                    {
                        await client.DeleteAsync($"{_config?["Plex:Url"]}/playlists/{playList.PlexId}/items/{del}?X-Plex-Token={_config?["Plex:Token"]}")
                    }

                    foreach (var add in toAdd)
                    {
                        await client.PutAsync($"{_config?["Plex:Url"]}/playlist/{playList.PlexId}/items?uri=server%3A%2F%2F{_config?["Plex:ServerId"]}%2Fcom.plexapp.plugins.library%2Flibrary%2Fmetadata%2F{add}&X-Plex-Token={_config?["Plex:Token"]}", null);
                    }

                    if (toAdd.Count > 0 || toDelete.Count > 0)
                    {
                        //update description and poster only for updated playlists
                        var poster = playList.PosterUrl;
                        await client.PostAsync($"{_config?["Plex:Url"]}/library/metadata/{playList.PlexId}/posters?url={poster}&X-Plex-Token={_config?["Plex:Token"]}", null);

                        await client.PutAsync($"{_config?["Plex:Url"]}/playlists/{playList.PlexId}?summary={playList.Description}&X-Plex-Token={_config?["Plex:Token"]}", null);

                    }

                    // foreach (var tpl in plexIds)
                    // {
                    //     var ft = tpl.Item2;
                    //     var key = tpl.Item1;
                    //     _logger?.LogInformation("Adding to Playlist (" + title + "): " + ft.Artists[0].Name + " - " + ft.Album.Name + " - " + ft.Name);
                    //     await client.PutAsync($"{_config?["Plex:Url"]}{playListKeys.Item2}?uri=server%3A%2F%2F{_config?["Plex:ServerId"]}%2Fcom.plexapp.plugins.library%2Flibrary%2Fmetadata%2F{key}&X-Plex-Token={_config?["Plex:Token"]}", null);
                    // }
                }
                else
                {
                    _logger?.LogError("No Titles found in Plex for Playlist " + playList.Name);
                }

                return report;
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
                    // await client.DeleteAsync($"{_config?["Plex:Url"]}{playListResult.Item2}?X-Plex-Token={_config?["Plex:Token"]}");
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



    }
}