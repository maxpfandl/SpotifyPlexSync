using System.Collections.Generic;
using Newtonsoft.Json;

public static class PlaylistExtractor
{
    public static void Extract()
    {
        string json = File.ReadAllText(@"D:\Development\SpotifyPlexSync\playlists.json");

        dynamic data = JsonConvert.DeserializeObject(json)!;

        List<Tuple<string, string>> results = new List<Tuple<string, string>>();

        List<string> items = new List<string>();

        foreach (var item in data!.contents.items)
        {
            items.Add(item.uri.ToString());
        }

        List<string> title = new List<string>();

        foreach (var item in data!.contents.metaItems)
        {
            title.Add(item.attributes.name.ToString());
        }

        for (int i = 0; i < items.Count; i++)
        {
            results.Add(new Tuple<string, string>(items[i].Split(':')[2], title[i]));
        }

        foreach (var result in results)
        {
            Console.WriteLine("\""+result.Item1 + "|" + result.Item2+"\",");
        }

    }
}