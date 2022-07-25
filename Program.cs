using System;
using Microsoft.Extensions.Configuration;
using SpotifyAPI.Web;
using System.IO;
using Microsoft.Extensions.Logging;
using Plex.ServerApi.Api;
using Plex.ServerApi.Clients;
using Plex.Library.Factories;
using Plex.ServerApi;
using System.Xml.Linq;
using System.Text.RegularExpressions;
using System.Web;

namespace SpotifyPlexSync
{
    internal class Program
    {
        static async Task Main(string[] args)
        {



            var builder = new ConfigurationBuilder()
             .AddJsonFile($"appsettings.json", true, true)
             .AddJsonFile($"appsettings.my.json", true, true);
            var config = builder.Build();


            var spotifyConfig = SpotifyClientConfig.CreateDefault();

            var request = new ClientCredentialsRequest(config["Spotify:ClientID"], config["Spotify:ClientSecret"]);
            var response = await new OAuthClient(spotifyConfig).RequestToken(request);

            var spotify = new SpotifyClient(spotifyConfig.WithToken(response.AccessToken));

            var playlists = config.GetSection("Sync").Get<List<string>>();

            foreach (var playlist in playlists)
            {

                var id = playlist.Split('|')[0];
                var spotifyPlaylist = await spotify.Playlists.Get(id);

                Console.WriteLine("Working on Spotifyplaylist: " + spotifyPlaylist.Name);
                await AdaptPlexPlaylist(spotifyPlaylist, config);
            }




        }

        private static async Task AdaptPlexPlaylist(FullPlaylist spotifyPl, IConfigurationRoot config)
        {
            string title = config["Prefix"] + spotifyPl.Name;
            using (HttpClient client = new HttpClient())
            {

                List<Tuple<string?, FullTrack>> plexIds = new List<Tuple<string?, FullTrack>>();

                foreach (var track in spotifyPl.Tracks?.Items!)
                {
                    var ft = track.Track as FullTrack;
                    if (ft != null)
                    {

                        var searchTerm = Regex.Replace(ft.Name, @"\(.*?\)", "").Trim(); // remove all in brackets
                        searchTerm = HttpUtility.UrlEncode(searchTerm);
                        var searchResult = await client.GetAsync(config["Plex:Url"] + $"/hubs/search?query={searchTerm}&limit=100&X-Plex-Token={config["Plex:Token"]}");

                        if (!searchResult.IsSuccessStatusCode)
                        {
                            Console.WriteLine("Error while searching " + searchTerm + "\n  " + searchResult.ReasonPhrase);
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
                                    plexIds.Add(new Tuple<string?, FullTrack>(key, ft));
                                    Console.ForegroundColor = ConsoleColor.Green;
                                    Console.WriteLine("Track found on Plex: \n  Spotify: " + ft.Artists[0].Name + " - " + ft.Album.Name + " - " + ft.Name + "\n  Plex:    " + plexArtist + " - " + plexAlbum + " - " + plexTitle);
                                    Console.ResetColor();
                                    found = true;
                                    break;
                                }
                            }


                        }
                        if (!found)
                        {
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            var text = ft.Artists[0].Name + " - " + ft.Album.Name + " - " + ft.Name + "||" + searchTerm;
                            Console.WriteLine("Track not found on Plex: " + text);
                            if (config["LogUnmatched"].ToLower() == "true")
                            {
                                File.AppendAllLines($"unmatched_{DateTime.Now.Ticks}.log", new List<string>() { text });
                            }
                            Console.ResetColor();
                        }

                    }

                }

                if (plexIds.Count > 0)
                {
                    string? playListLibKey = "";
                    string? playListKeyKey = "";
                    Console.WriteLine("Search for Playlist in Plex" + title);
                    var plexList = await client.GetAsync($"{config["Plex:Url"]}/playlists?title={title}&X-Plex-Token={config["Plex:Token"]}");

                    XDocument doc = XDocument.Parse(await plexList.Content.ReadAsStringAsync());

                    // clear playlist
                    if (doc.Descendants("Playlist").Count() == 1)
                    {
                        Console.WriteLine("Found Playlist: clearing Items");
                        foreach (var pl in doc.Descendants("Playlist"))
                        {
                            playListLibKey = pl.Attribute("key")?.Value;
                            playListKeyKey = pl.Attribute("ratingKey")?.Value;
                            await client.DeleteAsync($"{config["Plex:Url"]}{playListLibKey}?X-Plex-Token={config["Plex:Token"]}");
                        }
                    }
                    else if (doc.Descendants("Playlist").Count() == 0)
                    {
                        Console.WriteLine("Playlist not found: creating");
                        var result = await client.PostAsync($"{config["Plex:Url"]}/playlists?uri=server%3A%2F%{config["Plex:ServerId"]}%2Fcom.plexapp.plugins.library%2Flibrary%2Fmetadata%2F1109%2C1200&includeExternalMedia=1&title={title}&smart=0&type=audio&X-Plex-Token={config["Plex:Token"]}", null);

                        doc = XDocument.Parse(await result.Content.ReadAsStringAsync());
                        foreach (var pl in doc.Descendants("Playlist"))
                        {
                            playListLibKey = pl.Attribute("key")?.Value;
                            playListKeyKey = pl.Attribute("ratingKey")?.Value;
                        }

                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("PlaylistTitle ambiguous: " + title);
                        Console.ResetColor();
                    }



                    Console.WriteLine("PlaylistID in Plex: " + playListLibKey);

                    //update description and poster
                    if (spotifyPl?.Images?.Count > 0)
                    {
                        var poster = spotifyPl.Images[0].Url;
                        await client.PostAsync($"{config["Plex:Url"]}/library/metadata/{playListKeyKey}/posters?url={poster}&X-Plex-Token={config["Plex:Token"]}", null);
                    }

                    await client.PutAsync($"{config["Plex:Url"]}/playlists/{playListKeyKey}?summary={spotifyPl?.Description}&X-Plex-Token={config["Plex:Token"]}", null);

                    foreach (var tpl in plexIds)
                    {
                        var ft = tpl.Item2;
                        var key = tpl.Item1;
                        Console.WriteLine("Adding to Playlist: " + ft.Artists[0].Name + " - " + ft.Album.Name + " - " + ft.Name);
                        await client.PutAsync($"{config["Plex:Url"]}{playListLibKey}?uri=server%3A%2F%2F{config["Plex:ServerId"]}%2Fcom.plexapp.plugins.library%2Flibrary%2Fmetadata%2F{key}&X-Plex-Token={config["Plex:Token"]}", null);
                    }
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("No Titles found in Plex for Playlist " + title);
                    Console.ResetColor();
                }


            }

        }
        private static bool Compare(FullTrack track, string plexTitle, string plexArtist)
        {

            var spTitle = Regex.Replace(track.Name, @"[^0-9a-zA-Z:,]+", "").ToLower();
            var spArtist = Regex.Replace(track.Artists[0].Name, @"[^0-9a-zA-Z:,]+", "").ToLower();
            var plexTitleNorm = Regex.Replace(plexTitle, @"[^0-9a-zA-Z:,]+", "").ToLower();
            var plexArtistNorm = Regex.Replace(plexArtist, @"[^0-9a-zA-Z:,]+", "").ToLower();

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