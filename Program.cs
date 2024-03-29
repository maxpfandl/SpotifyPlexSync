﻿using Microsoft.Extensions.Configuration;
using SpotifyAPI.Web;
using System.Xml.Linq;
using System.Text.RegularExpressions;
using System.Web;
using Microsoft.Extensions.Logging;
using SpotifyAPI.Web.Auth;
using System.Text;
using Newtonsoft.Json;
using Swan.Parsers;
using Newtonsoft.Json.Linq;
using QuickType;
using Swan.Formatters;
using System.Text.Json;
using Newtonsoft.Json.Serialization;


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
             .AddJsonFile($"appsettings.my.json", true, true)
             .AddJsonFile($"appsettings.playlists.json", true, true);
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
                List<string> playlists = new List<string>();
                if (_args != null && _args.Count() > 0 && (_args[0] == "lidarr" || _args[0] == "lidarrnew"))
                {
                    playlists = await GetPlaylistsFromLidarr(_config?["Lidarr:Url"], _config?["Lidarr:ApiKey"]);
                }
                else
                {
                    playlists = _config?.GetSection("Sync").Get<List<string>>()!;
                }
                var maxTracks = _config?.GetValue<int>("MaxTracks");
                // single list
                reports.Add("Starting " + DateTime.Now.ToString("G"));
                if (playlistId != null && playlistId != "new" && playlistId != "lidarr" && playlistId != "lidarrnew")
                {
                    var spotifyPlaylist = await _spotify!.Playlists.Get(playlistId);
                    _logger?.LogInformation("Working on Spotifyplaylist: " + spotifyPlaylist.Name);
                    var report = await CreateOrUpdatePlexPlayList(spotifyPlaylist, checkSnapshot: bool.Parse(_config!["CheckSpotifySnapshot"]));
                    if (!String.IsNullOrEmpty(report))
                        reports.Add(report);
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

                            if (spotifyPlaylist != null)
                            {
                                if (maxTracks == 0 || spotifyPlaylist.Tracks?.Total <= maxTracks)
                                {
                                    spPlaylists.Add(spotifyPlaylist);
                                }
                                else
                                {
                                    reports.Add($"{playlist}: Too Many Tracks! " + spotifyPlaylist.Tracks?.Total + "/" + maxTracks);
                                }
                            }
                            else
                            {
                                reports.Add($"{playlist}: Fatal Error");
                            }

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
                            var report = await CreateOrUpdatePlexPlayList(playList, (playlistId == "new" || playlistId == "lidarrnew"), checkSnapshot: bool.Parse(_config!["CheckSpotifySnapshot"]));
                            if (!String.IsNullOrEmpty(report))
                                reports.Add(report);

                            // refresh client
                            var spotifyConfig = SpotifyClientConfig.CreateDefault();
                            var request = new ClientCredentialsRequest(_config?["Spotify:ClientID"]!, _config?["Spotify:ClientSecret"]!);
                            var response = await new OAuthClient(spotifyConfig).RequestToken(request);
                            _spotify = new SpotifyClient(spotifyConfig.WithToken(response.AccessToken));

                        }
                        catch (Exception ex)
                        {
                            _logger?.LogError("Syncing with Plex failed", ex);
                            reports.Add($"{playList.Name}: Fatal Error");
                            if (!await CheckPlexRunning())
                            {
                                return;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError("Getting Playlists from Spotify failed", ex);
                reports.Add("Ending " + DateTime.Now.ToString("G"));
            }
            reports.Add("Ending " + DateTime.Now.ToString("G"));

            if (reports.Count > 30)
            {
                int i = 0;
                int chunkSize = 30;
                string[][] result = reports.GroupBy(s => i++ / chunkSize).Select(g => g.ToArray()).ToArray();
                foreach (var resultarr in result)
                {
                    var message = String.Join(Environment.NewLine, resultarr);
                    await SendToWebhook(_config.GetValue<string>("WebHook"), _config.GetValue<string>("WebHookBasicAuth"), message);
                    _logger?.LogInformation(message);
                }
            }
            else
            {
                var message = String.Join(Environment.NewLine, reports);
                await SendToWebhook(_config.GetValue<string>("WebHook"), _config.GetValue<string>("WebHookBasicAuth"), message);
                _logger?.LogInformation(message);
            }



        }

        private async static Task<List<string>> GetPlaylistsFromLidarr(string? url, string? apiKey)
        {
            var result = new List<string>();
            var endpoint = url + "/api/v1/importlist";
            var requestMovie = new HttpRequestMessage(HttpMethod.Get, endpoint);
            requestMovie.Headers.Add("X-Api-Key", apiKey);
            using (HttpClient client = new HttpClient())
            {
                var resultMovie = await client.SendAsync(requestMovie);
                if (resultMovie.IsSuccessStatusCode)
                {
                    string payload = await (new StreamReader(resultMovie.Content.ReadAsStream())).ReadToEndAsync();



                    var root = Json.Deserialize<ImportList[]>(payload);



                    foreach (var list in root!)
                    {
                        if (list.name == "Spotify Playlists")
                        {
                            foreach (var field in list.fields)
                            {
                                if (field.name == "playlistIds")
                                {
                                    var ids = field.value.ToList();
                                    return ids;

                                }
                            }
                        }
                    }

                    // dynamic lists = JArray.Parse(payload);
                    // foreach (dynamic list in lists)
                    // { 
                    //     if(list.name == "Spotify Playlists")
                    //     {

                    //         dynamic fields = JObject.Parse(list.fields);
                    //         foreach(var field in fields){
                    //             if(field.name=="playlistIds"){
                    //                 dynamic playlistIds = JArray.Parse(field.value);
                    //                 foreach(var playlist in playlistIds){
                    //                     result.Add(playlist);
                    //                     Console.WriteLine(playlist);
                    //                 }
                    //             }
                    //         }
                    //     }
                    // }




                }


            }

            return result;
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

                                try
                                {
                                    _logger?.LogInformation("Working on Spotifyplaylist: " + playlist.Name);
                                    var report = await CreateOrUpdatePlexPlayList(playlist);
                                    if (!String.IsNullOrEmpty(report))
                                        reports.Add(report);
                                }
                                catch (Exception ex)
                                {
                                    _logger?.LogError($"Error: Playlist {playlist.Name} not updated", ex);
                                    if (!await CheckPlexRunning())
                                    {
                                        return;
                                    }
                                }
                            }

                        }
                        // someting to test...
                        if (_args[0] == "extract")
                        {
                            var playlists = await _spotify.Playlists.CurrentUsers();

                            List<FullPlaylist> fullPlayLists = new List<FullPlaylist>();

                            List<string> playlistsJson = new List<string>();

                            await foreach (var playlist in _spotify!.Paginate(playlists))
                            {
                                try
                                {
                                    var fplst = await _spotify.Playlists.Get(playlist.Id);
                                    if (fplst != null && fplst.Tracks!.Items!.Count > 0)
                                    {

                                        if (_config.GetValue<bool>("AddAuthorToTitle") && !string.IsNullOrEmpty(playlist.Owner.DisplayName))
                                        {
                                            playlistsJson.Add($"{playlist.Id}|{playlist.Name} by {playlist.Owner.DisplayName}");
                                            Console.WriteLine($"\"{playlist.Id}|{playlist.Name} by {playlist.Owner.DisplayName}\",");
                                        }
                                        else
                                        {
                                            playlistsJson.Add($"{playlist.Id}|{playlist.Name}");
                                            Console.WriteLine($"\"{playlist.Id}|{playlist.Name}\",");
                                        }
                                    }
                                    else
                                    {
                                        Console.WriteLine($"\"ERROR: {playlist!.Id}|{playlist.Name}\",");
                                    }
                                }
                                catch
                                {
                                    Console.WriteLine($"\"ERROR: {playlist!.Id}|{playlist.Name}\",");
                                }



                            }
                            var jsonConfig = new { Sync = playlistsJson };
                            string json = JsonConvert.SerializeObject(jsonConfig);
                            File.WriteAllText("appsettings.playlists.json", json);

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

        private static async Task SendToWebhook(string webHook, string auth, string message)
        {
            if (message.Length > 4000)
                throw new ApplicationException("Webhook message too long (>4000 chars)");

            if (!String.IsNullOrEmpty(webHook))
            {
                using (HttpClient client = new HttpClient())
                {
                    if (!String.IsNullOrEmpty(auth))
                    {
                        var byteArray = Encoding.ASCII.GetBytes("madmap:IHU62zxIWVQfagUgi5DY");
                        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));
                    }
                    string json = $"{{\"event\":\"SpotifyPlexSync\",\"message\":\"{message}\"}}";
                    await client.PostAsync(webHook, new StringContent(json, Encoding.UTF8, "application/json"));
                }
            }

        }

        private static async Task<bool> CheckPlexRunning()
        {
            using (HttpClient client = new HttpClient())
            {
                var result = await client.GetAsync($"{_config?["Plex:Url"]}/activities?X-Plex-Token={_config?["Plex:Token"]}");
                if (!result.IsSuccessStatusCode)
                {
                    _logger?.LogError("Plex seems to be unavailable, exiting");
                    return false;
                }
                return true;
            }
        }

        private static async Task OnErrorReceived(object sender, string error, string state)
        {
            Console.WriteLine($"Aborting authorization, error received: {error}");
            await _server!.Stop();
        }


        private static async Task<string> CreateOrUpdatePlexPlayList(FullPlaylist spotifyPl, bool newOnly = false, bool checkSnapshot = true)
        {
            string report = "";

            SyncPlaylist playList = new SyncPlaylist(_config!, _logger!);

            using (HttpClient client = new HttpClient())
            {

                await playList.Initialize(spotifyPl, client, _spotify!, newOnly, checkSnapshot);

                report = playList.GetReport();

                _logger?.LogInformation(report);



                if (playList.HasFoundTracks)
                {



                    playList.PlexId = await GetPlaylist(playList.Name!, client);

                    // new playlist
                    if (playList.PlexId == null)
                    {
                        playList.PlexId = await CreatePlayListPlex(playList.Name!, client);

                        // var poster = playList.PosterUrl;
                        // await client.PostAsync($"{_config?["Plex:Url"]}/library/metadata/{playList.PlexId}/posters?url={poster}&X-Plex-Token={_config?["Plex:Token"]}", null);
                        // await client.PutAsync($"{_config?["Plex:Url"]}/playlists/{playList.PlexId}?summary={playList.Description}&X-Plex-Token={_config?["Plex:Token"]}", null);

                        foreach (var track in playList.Tracks)
                        {
                            if (track.PTrackKey != null)
                            {
                                _logger?.LogInformation("Adding to Playlist (" + playList.Name + "): " + track.SpTrack?.Artists[0].Name + " - " + track.SpTrack?.Album.Name + " - " + track.SpTrack?.Name);
                                await client.PutAsync($"{_config?["Plex:Url"]}/playlists/{playList.PlexId}/items?uri=server%3A%2F%2F{_config?["Plex:ServerId"]}%2Fcom.plexapp.plugins.library%2Flibrary%2Fmetadata%2F{track.PTrackKey}&X-Plex-Token={_config?["Plex:Token"]}", null);
                            }
                        }
                        report += " | new";
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
                            // var poster = playList.PosterUrl;
                            // await client.PostAsync($"{_config?["Plex:Url"]}/library/metadata/{playList.PlexId}/posters?url={poster}&X-Plex-Token={_config?["Plex:Token"]}", null);
                            // await client.PutAsync($"{_config?["Plex:Url"]}/playlists/{playList.PlexId}?summary={playList.Description}&X-Plex-Token={_config?["Plex:Token"]}", null);
                            foreach (var track in playList.Tracks)
                            {
                                if (track.PTrackKey != null)
                                {
                                    _logger?.LogInformation("Adding to Playlist (" + playList.Name + "): " + track.SpTrack?.Artists[0].Name + " - " + track.SpTrack?.Album.Name + " - " + track.SpTrack?.Name);
                                    await client.PutAsync($"{_config?["Plex:Url"]}/playlists/{playList.PlexId}/items?uri=server%3A%2F%2F{_config?["Plex:ServerId"]}%2Fcom.plexapp.plugins.library%2Flibrary%2Fmetadata%2F{track.PTrackKey}&X-Plex-Token={_config?["Plex:Token"]}", null);
                                }
                            }
                            report += " | recreated";


                        }
                        else
                        {
                            report += " | no change";
                            _logger?.LogInformation("No change to Playlist: " + playList.Name);
                        }

                    }

                    //Update Poster + Desc
                    if (_config.GetValue<Boolean>("AddReportToDescription"))
                    {
                        report += " | " + DateTime.Now.ToString("yyyy-MM-dd HH:mm");
                        playList.AddToDescription(report);
                    }

                    await client.PostAsync($"{_config?["Plex:Url"]}/library/metadata/{playList.PlexId}/posters?url={HttpUtility.UrlEncode(playList.PosterUrl)}&X-Plex-Token={_config?["Plex:Token"]}", null);
                    await client.PutAsync($"{_config?["Plex:Url"]}/playlists/{playList.PlexId}?summary={HttpUtility.UrlEncode(playList.Description)}&X-Plex-Token={_config?["Plex:Token"]}", null);


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
            var encName = HttpUtility.UrlEncode(name);
            var result = await client.PostAsync($"{_config?["Plex:Url"]}/playlists?uri=server%3A%2F%{_config?["Plex:ServerId"]}%2Fcom.plexapp.plugins.library%2Flibrary%2Fmetadata%2F1109%2C1200&includeExternalMedia=1&title={encName}&smart=0&type=audio&X-Plex-Token={_config?["Plex:Token"]}", null);

            XDocument doc = XDocument.Parse(await result.Content.ReadAsStringAsync());

            foreach (var pl in doc.Descendants("Playlist"))
            {
                var key = pl.Attribute("ratingKey")?.Value;
                if (key != null)
                    return key;
            }
            return null;
        }

        public static async Task<string?> GetPlaylist(string title, HttpClient client)
        {

            _logger?.LogInformation("Search for Playlist in Plex: " + title);
            var enctitle = HttpUtility.UrlEncode(title);
            var plexList = await client.GetAsync($"{_config?["Plex:Url"]}/playlists?title={enctitle}&X-Plex-Token={_config?["Plex:Token"]}");

            XDocument doc = XDocument.Parse(await plexList.Content.ReadAsStringAsync());

            if (doc.Descendants("Playlist").Count() > 0)
            {
                foreach (var playlist in doc.Descendants("Playlist"))
                {
                    if (playlist.Attribute("title")?.Value == title)
                        return playlist.Attribute("ratingKey")?.Value;
                }
                throw new ApplicationException("PlaylistTitle ambiguous");
            }
            return null;
        }



    }
}