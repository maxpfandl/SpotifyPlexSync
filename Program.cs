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

                        var searchResult = await client.GetAsync(config["Plex:Url"] + $"/library/sections/{config["Plex:LibraryKey"]}/all?type=10&title={ft.Name}&X-Plex-Token={config["Plex:Token"]}");

                        XDocument doc = XDocument.Parse(await searchResult.Content.ReadAsStringAsync());
                        var found = false;
                        foreach (var pl in doc.Descendants("Track"))
                        {

                            var key = pl.Attribute("ratingKey")?.Value;
                            var plextitle = pl.Attribute("title")?.Value;
                            var artist = pl.Attribute("grandparentTitle")?.Value;

                            if (artist!.ToLower().Contains(ft.Artists[0].Name.ToLower()) && ft.Name.ToLower() == plextitle!.ToLower())
                            {
                                plexIds.Add(new Tuple<string?, FullTrack>(key, ft));
                                Console.ForegroundColor = ConsoleColor.Green;
                                Console.WriteLine("Track found on Plex: " + ft.Artists[0].Name + "||" + ft.Album.Name + "||" + ft.Name);
                                Console.ResetColor();
                                found = true;
                                break;
                            }


                        }
                        if (!found)
                        {
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            Console.WriteLine("Track not found on Plex: " + ft.Artists[0].Name + "||" + ft.Album.Name + "||" + ft.Name);
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
                        Console.WriteLine("Adding to Playlist: " + ft.Artists[0].Name + "||" + ft.Album.Name + "||" + ft.Name);
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
    }
}