using Microsoft.Extensions.Configuration;
using SpotifyAPI.Web;
using System.Xml.Linq;
using System.Text.RegularExpressions;
using System.Web;
using Microsoft.Extensions.Logging;
using SpotifyAPI.Web.Auth;

namespace SpotifyPlexSync
{
    internal class Program
    {
        private static ILogger? _logger;
        private static IConfiguration? _config;

        private static SpotifyClient? _spotify;

        private static EmbedIOAuthServer? _server;

        private static string[]? _args;
        private static TaskCompletionSource<bool> _tcs = new TaskCompletionSource<bool>();

        static async Task Main(string[] args)
        {

            _args = args;

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



            if (_args != null && args.Length == 1 && (args[0] == "all" || args[0] == "extract"))
            {
                // Make sure "http://localhost:5000/callback" is in your spotify application as redirect uri!
                _server = new EmbedIOAuthServer(new Uri("http://localhost:5000/callback"), 5000);
                await _server.Start();

                _server.AuthorizationCodeReceived += OnAuthorizationCodeReceived;
                _server.ErrorReceived += OnErrorReceived!;

                var request = new LoginRequest(_server.BaseUri, _config?["Spotify:ClientID"]!, LoginRequest.ResponseType.Code)
                {
                    Scope = new List<string> { Scopes.UserReadEmail, Scopes.PlaylistReadPrivate, Scopes.PlaylistReadCollaborative }
                };
                try
                {
                    BrowserUtil.Open(request.ToUri());
                }
                catch (Exception)
                {
                    _logger.LogError("Unable to open URL, manually open: {0}", request.ToUri());
                }
                _logger.LogInformation("Waiting 5 min for authentication");
                await Task.Delay(5 * 60 * 1000);
                await _tcs.Task;

            }
            else
            {
                var spotifyConfig = SpotifyClientConfig.CreateDefault();
                var request = new ClientCredentialsRequest(_config?["Spotify:ClientID"]!, _config?["Spotify:ClientSecret"]!);
                var response = await new OAuthClient(spotifyConfig).RequestToken(request);
                _spotify = new SpotifyClient(spotifyConfig.WithToken(response.AccessToken));
                if (_args != null && args.Length == 1)
                {
                    await WorkOnConfiguredPlaylists(_args[0]);
                }
                else
                {
                    await WorkOnConfiguredPlaylists();
                }
            }

        }

        private static async Task WorkOnConfiguredPlaylists(string? playlistId = null)
        {
            List<string> reports = new List<string>();
            try
            {
                var playlists = _config?.GetSection("Sync").Get<List<string>>();
                // single list

                if (playlistId != null)
                {
                    var spotifyPlaylist = await _spotify!.Playlists.Get(playlistId);
                    _logger?.LogInformation("Working on Spotifyplaylist: " + spotifyPlaylist.Name);
                    reports.Add(await CreateOrUpdatePlexPlayList(spotifyPlaylist));
                }
                else
                {
                    List<FullPlaylist> spPlaylists = new List<FullPlaylist>();
                    foreach (var playlist in playlists!)
                    {
                        try
                        {
                            var id = playlist.Split('|')[0];
                            var spotifyPlaylist = await _spotify!.Playlists.Get(id);
                            spPlaylists.Add(spotifyPlaylist);

                        }
                        catch (Exception ex)
                        {
                            _logger?.LogError("Getting Playlists from Spotify failed", ex);
                        }
                    }

                    foreach (var playList in spPlaylists)
                    {
                        try
                        {
                            _logger?.LogInformation("Working on Spotifyplaylist: " + playList.Name);

                            reports.Add(await CreateOrUpdatePlexPlayList(playList));
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogError("Syncing with Plex failed", ex);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError("Getting Playlists from Spotify failed", ex);
            }

            var message = String.Join(Environment.NewLine, reports);

            _logger?.LogInformation(message);
        }

        private static async Task OnAuthorizationCodeReceived(object sender, AuthorizationCodeResponse response)
        {
            await _server!.Stop();

            var config = SpotifyClientConfig.CreateDefault();
            var oauth = new OAuthClient(config);
            var tokenResponse = await oauth.RequestToken(
              new AuthorizationCodeTokenRequest(
                _config?["Spotify:ClientID"]!, _config?["Spotify:ClientSecret"]!, response.Code, new Uri("http://localhost:5000/callback")
              )
            );

            _spotify = new SpotifyClient(tokenResponse.AccessToken);
            // do calls with Spotify and save token?

            List<string> reports = new List<string>();

            try
            {
                if (_args != null && _args.Length == 1 && !String.IsNullOrEmpty(_args[0]))
                {
                    try
                    {
                        if (_args[0] == "all")
                        {
                            var playlists = await _spotify.Playlists.CurrentUsers();

                            List<FullPlaylist> fullPlayLists = new List<FullPlaylist>();

                            await foreach (var playlist in _spotify!.Paginate(playlists))
                            {
                                _logger?.LogInformation("Getting Playlist: " + playlist.Name);
                                fullPlayLists.Add(await _spotify.Playlists.Get(playlist.Id));

                            }
                            foreach (var playlist in fullPlayLists)
                            {
                                _logger?.LogInformation("Working on Spotifyplaylist: " + playlist.Name);
                                reports.Add(await CreateOrUpdatePlexPlayList(playlist));
                            }

                        }
                        // someting to test...
                        if (_args[0] == "extract")
                        {
                            var playlists = await _spotify.Playlists.CurrentUsers();

                            List<FullPlaylist> fullPlayLists = new List<FullPlaylist>();

                            await foreach (var playlist in _spotify!.Paginate(playlists))
                            {
                                Console.WriteLine($"\"{playlist.Id}|{playlist.Name}\",");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError("CreateUpdate Playlist in Plex failed", ex);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError("Getting Playlists from Spotify failed", ex);
            }

            var message = String.Join(Environment.NewLine, reports);

            _logger?.LogInformation(message);
            _tcs.SetResult(true);
        }

        private static async Task OnErrorReceived(object sender, string error, string state)
        {
            Console.WriteLine($"Aborting authorization, error received: {error}");
            await _server!.Stop();
        }


        private static async Task<string> CreateOrUpdatePlexPlayList(FullPlaylist spotifyPl)
        {
            string report = "";

            SyncPlaylist playList = new SyncPlaylist(_config!, _logger!);

            using (HttpClient client = new HttpClient())
            {

                await playList.Initialize(spotifyPl, client, _spotify!);

                report = playList.GetReport();
                _logger?.LogInformation(report);


                if (playList.HasFoundTracks)
                {
                    playList.PlexId = await GetPlaylist(playList.Name!, client);

                    // new playlist
                    if (playList.PlexId == null)
                    {
                        playList.PlexId = await CreatePlayListPlex(playList.Name!, client);
                        var poster = playList.PosterUrl;
                        await client.PostAsync($"{_config?["Plex:Url"]}/library/metadata/{playList.PlexId}/posters?url={poster}&X-Plex-Token={_config?["Plex:Token"]}", null);

                        await client.PutAsync($"{_config?["Plex:Url"]}/playlists/{playList.PlexId}?summary={playList.Description}&X-Plex-Token={_config?["Plex:Token"]}", null);
                        foreach (var track in playList.Tracks)
                        {
                            if (track.PTrackKey != null)
                            {
                                _logger?.LogInformation("Adding to Playlist (" + playList.Name + "): " + track.SpTrack?.Artists[0].Name + " - " + track.SpTrack?.Album.Name + " - " + track.SpTrack?.Name);
                                await client.PutAsync($"{_config?["Plex:Url"]}/playlists/{playList.PlexId}/items?uri=server%3A%2F%2F{_config?["Plex:ServerId"]}%2Fcom.plexapp.plugins.library%2Flibrary%2Fmetadata%2F{track.PTrackKey}&X-Plex-Token={_config?["Plex:Token"]}", null);
                            }
                        }
                        report += " - new";
                    }


                    // existing playlist
                    else
                    {
                        var existingTracks = await client.GetAsync($"{_config?["Plex:Url"]}/playlists/{playList.PlexId}/items?X-Plex-Token={_config?["Plex:Token"]}");

                        List<string> existingKeys = new List<string>();

                        XDocument doc = XDocument.Parse(await existingTracks.Content.ReadAsStringAsync());

                        foreach (var pl in doc.Descendants("Track"))
                        {
                            var key = pl.Attribute("ratingKey")?.Value;
                            if (key != null)
                                existingKeys.Add(key);

                        }


                        bool recreate = false;

                        foreach (var fromSpotify in playList.Tracks)
                        {
                            if (fromSpotify.PTrackKey != null)
                            {
                                var existingItem = existingKeys.Find(p => p == fromSpotify.PTrackKey);
                                if (existingItem == null)
                                    recreate = true;
                            }
                        }

                        foreach (var existing in existingKeys)
                        {
                            var deleteItem = playList.Tracks.Find(p => p.PTrackKey == existing);
                            if (deleteItem == null)
                                recreate = true;
                        }

                        if (recreate)
                        {
                            // delete current playlist and recreate to show up as new
                            await client.DeleteAsync($"{_config?["Plex:Url"]}/playlists/{playList.PlexId}?X-Plex-Token={_config?["Plex:Token"]}");

                            playList.PlexId = await CreatePlayListPlex(playList.Name!, client);

                            //update description and poster only for updated playlists
                            var poster = playList.PosterUrl;
                            await client.PostAsync($"{_config?["Plex:Url"]}/library/metadata/{playList.PlexId}/posters?url={poster}&X-Plex-Token={_config?["Plex:Token"]}", null);

                            await client.PutAsync($"{_config?["Plex:Url"]}/playlists/{playList.PlexId}?summary={playList.Description}&X-Plex-Token={_config?["Plex:Token"]}", null);
                            foreach (var track in playList.Tracks)
                            {
                                if (track.PTrackKey != null)
                                {
                                    _logger?.LogInformation("Adding to Playlist (" + playList.Name + "): " + track.SpTrack?.Artists[0].Name + " - " + track.SpTrack?.Album.Name + " - " + track.SpTrack?.Name);
                                    await client.PutAsync($"{_config?["Plex:Url"]}/playlists/{playList.PlexId}/items?uri=server%3A%2F%2F{_config?["Plex:ServerId"]}%2Fcom.plexapp.plugins.library%2Flibrary%2Fmetadata%2F{track.PTrackKey}&X-Plex-Token={_config?["Plex:Token"]}", null);
                                }
                            }
                            report += " - recreated";


                        }
                        else
                            report += " - no change";

                    }

                }
                else
                {
                    _logger?.LogError("No Titles found in Plex for Playlist " + playList.Name);
                }

                return report;
            }

        }

        private static async Task<string?> CreatePlayListPlex(string name, HttpClient client)
        {
            var result = await client.PostAsync($"{_config?["Plex:Url"]}/playlists?uri=server%3A%2F%{_config?["Plex:ServerId"]}%2Fcom.plexapp.plugins.library%2Flibrary%2Fmetadata%2F1109%2C1200&includeExternalMedia=1&title={name}&smart=0&type=audio&X-Plex-Token={_config?["Plex:Token"]}", null);

            XDocument doc = XDocument.Parse(await result.Content.ReadAsStringAsync());

            foreach (var pl in doc.Descendants("Playlist"))
            {
                var key = pl.Attribute("ratingKey")?.Value;
                if (key != null)
                    return key;
            }
            return null;
        }

        private static async Task<string?> GetPlaylist(string title, HttpClient client)
        {

            _logger?.LogInformation("Search for Playlist in Plex" + title);
            var plexList = await client.GetAsync($"{_config?["Plex:Url"]}/playlists?title={title}&X-Plex-Token={_config?["Plex:Token"]}");

            XDocument doc = XDocument.Parse(await plexList.Content.ReadAsStringAsync());

            if (doc.Descendants("Playlist").Count() == 1)
            {
                foreach (var pl in doc.Descendants("Playlist"))
                {
                    return pl.Attribute("ratingKey")?.Value;
                }
            }
            else if (doc.Descendants("Playlist").Count() > 1)
            {
                throw new ApplicationException("PlaylistTitle ambiguous");
            }
            return null;
        }



    }
}