// <auto-generated />
//
// To parse this JSON data, add NuGet 'Newtonsoft.Json' then do:
//
//    using QuickType;
//
//    var importList = ImportList.FromJson(jsonString);

namespace QuickType
{
    using System;
    using System.Collections.Generic;

    using System.Globalization;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;

    public partial class ImportList
    {
        [JsonProperty("enableAutomaticAdd", NullValueHandling = NullValueHandling.Ignore)]
        public bool? EnableAutomaticAdd { get; set; }

        [JsonProperty("shouldMonitor", NullValueHandling = NullValueHandling.Ignore)]
        public ShouldMonitor? ShouldMonitor { get; set; }

        [JsonProperty("shouldMonitorExisting", NullValueHandling = NullValueHandling.Ignore)]
        public bool? ShouldMonitorExisting { get; set; }

        [JsonProperty("shouldSearch", NullValueHandling = NullValueHandling.Ignore)]
        public bool? ShouldSearch { get; set; }

        [JsonProperty("rootFolderPath", NullValueHandling = NullValueHandling.Ignore)]
        public RootFolderPath? RootFolderPath { get; set; }

        [JsonProperty("monitorNewItems", NullValueHandling = NullValueHandling.Ignore)]
        public MonitorNewItems? MonitorNewItems { get; set; }

        [JsonProperty("qualityProfileId", NullValueHandling = NullValueHandling.Ignore)]
        public long? QualityProfileId { get; set; }

        [JsonProperty("metadataProfileId", NullValueHandling = NullValueHandling.Ignore)]
        public long? MetadataProfileId { get; set; }

        [JsonProperty("listType", NullValueHandling = NullValueHandling.Ignore)]
        public ListType? ListType { get; set; }

        [JsonProperty("listOrder", NullValueHandling = NullValueHandling.Ignore)]
        public long? ListOrder { get; set; }

        [JsonProperty("minRefreshInterval", NullValueHandling = NullValueHandling.Ignore)]
        public DateTimeOffset? MinRefreshInterval { get; set; }

        [JsonProperty("name", NullValueHandling = NullValueHandling.Ignore)]
        public string Name { get; set; }

        [JsonProperty("fields", NullValueHandling = NullValueHandling.Ignore)]
        public List<Field> Fields { get; set; }

        [JsonProperty("implementationName", NullValueHandling = NullValueHandling.Ignore)]
        public ImplementationName? ImplementationName { get; set; }

        [JsonProperty("implementation", NullValueHandling = NullValueHandling.Ignore)]
        public Implementation? Implementation { get; set; }

        [JsonProperty("configContract", NullValueHandling = NullValueHandling.Ignore)]
        public ConfigContract? ConfigContract { get; set; }

        [JsonProperty("infoLink", NullValueHandling = NullValueHandling.Ignore)]
        public Uri InfoLink { get; set; }

        [JsonProperty("tags", NullValueHandling = NullValueHandling.Ignore)]
        public List<long> Tags { get; set; }

        [JsonProperty("id", NullValueHandling = NullValueHandling.Ignore)]
        public long? Id { get; set; }
    }

    public partial class Field
    {
        [JsonProperty("order", NullValueHandling = NullValueHandling.Ignore)]
        public long? Order { get; set; }

        [JsonProperty("name", NullValueHandling = NullValueHandling.Ignore)]
        public string Name { get; set; }

        [JsonProperty("label", NullValueHandling = NullValueHandling.Ignore)]
        public string Label { get; set; }

        [JsonProperty("helpText", NullValueHandling = NullValueHandling.Ignore)]
        public HelpText? HelpText { get; set; }

        [JsonProperty("value", NullValueHandling = NullValueHandling.Ignore)]
        public Value? Value { get; set; }

        [JsonProperty("type", NullValueHandling = NullValueHandling.Ignore)]
        public TypeEnum? Type { get; set; }

        [JsonProperty("advanced", NullValueHandling = NullValueHandling.Ignore)]
        public bool? Advanced { get; set; }

        [JsonProperty("isFloat", NullValueHandling = NullValueHandling.Ignore)]
        public bool? IsFloat { get; set; }

        [JsonProperty("hidden", NullValueHandling = NullValueHandling.Ignore)]
        public string Hidden { get; set; }
    }

    public enum ConfigContract { LastFmTagSettings, SpotifyFollowedArtistsSettings, SpotifyPlaylistSettings, SpotifySavedAlbumsSettings };

    public enum HelpText { NumberOfResultsToPullFromListMax1000, TagToPullArtistsFrom };

    public enum TypeEnum { Number, OAuth, Playlist, Textbox };

    public enum Implementation { LastFmTag, SpotifyFollowedArtists, SpotifyPlaylist, SpotifySavedAlbums };

    public enum ImplementationName { LastFmTag, SpotifyFollowedArtists, SpotifyPlaylists, SpotifySavedAlbums };

    public enum ListType { LastFm, Spotify };

    public enum MonitorNewItems { All };

    public enum RootFolderPath { Music, RootFolderPathMusic };

    public enum ShouldMonitor { EntireArtist };

    public partial struct Value
    {
        public long? Integer;
        public string String;
        public List<string> StringArray;

        public static implicit operator Value(long Integer) => new Value { Integer = Integer };
        public static implicit operator Value(string String) => new Value { String = String };
        public static implicit operator Value(List<string> StringArray) => new Value { StringArray = StringArray };
    }

    public partial class ImportList
    {
        public static List<ImportList> FromJson(string json) => JsonConvert.DeserializeObject<List<ImportList>>(json, QuickType.Converter.Settings);
    }

    public static class Serialize
    {
        public static string ToJson(this List<ImportList> self) => JsonConvert.SerializeObject(self, QuickType.Converter.Settings);
    }

    internal static class Converter
    {
        public static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
            DateParseHandling = DateParseHandling.None,
            Converters =
            {
                ConfigContractConverter.Singleton,
                HelpTextConverter.Singleton,
                TypeEnumConverter.Singleton,
                ValueConverter.Singleton,
                ImplementationConverter.Singleton,
                ImplementationNameConverter.Singleton,
                ListTypeConverter.Singleton,
                MonitorNewItemsConverter.Singleton,
                RootFolderPathConverter.Singleton,
                ShouldMonitorConverter.Singleton,
                new IsoDateTimeConverter { DateTimeStyles = DateTimeStyles.AssumeUniversal }
            },
        };
    }

    internal class ConfigContractConverter : JsonConverter
    {
        public override bool CanConvert(Type t) => t == typeof(ConfigContract) || t == typeof(ConfigContract?);

        public override object ReadJson(JsonReader reader, Type t, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null) return null;
            var value = serializer.Deserialize<string>(reader);
            switch (value)
            {
                case "LastFmTagSettings":
                    return ConfigContract.LastFmTagSettings;
                case "SpotifyFollowedArtistsSettings":
                    return ConfigContract.SpotifyFollowedArtistsSettings;
                case "SpotifyPlaylistSettings":
                    return ConfigContract.SpotifyPlaylistSettings;
                case "SpotifySavedAlbumsSettings":
                    return ConfigContract.SpotifySavedAlbumsSettings;
            }
            throw new Exception("Cannot unmarshal type ConfigContract");
        }

        public override void WriteJson(JsonWriter writer, object untypedValue, JsonSerializer serializer)
        {
            if (untypedValue == null)
            {
                serializer.Serialize(writer, null);
                return;
            }
            var value = (ConfigContract)untypedValue;
            switch (value)
            {
                case ConfigContract.LastFmTagSettings:
                    serializer.Serialize(writer, "LastFmTagSettings");
                    return;
                case ConfigContract.SpotifyFollowedArtistsSettings:
                    serializer.Serialize(writer, "SpotifyFollowedArtistsSettings");
                    return;
                case ConfigContract.SpotifyPlaylistSettings:
                    serializer.Serialize(writer, "SpotifyPlaylistSettings");
                    return;
                case ConfigContract.SpotifySavedAlbumsSettings:
                    serializer.Serialize(writer, "SpotifySavedAlbumsSettings");
                    return;
            }
            throw new Exception("Cannot marshal type ConfigContract");
        }

        public static readonly ConfigContractConverter Singleton = new ConfigContractConverter();
    }

    internal class HelpTextConverter : JsonConverter
    {
        public override bool CanConvert(Type t) => t == typeof(HelpText) || t == typeof(HelpText?);

        public override object ReadJson(JsonReader reader, Type t, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null) return null;
            var value = serializer.Deserialize<string>(reader);
            switch (value)
            {
                case "Number of results to pull from list (Max 1000)":
                    return HelpText.NumberOfResultsToPullFromListMax1000;
                case "Tag to pull artists from":
                    return HelpText.TagToPullArtistsFrom;
            }
            throw new Exception("Cannot unmarshal type HelpText");
        }

        public override void WriteJson(JsonWriter writer, object untypedValue, JsonSerializer serializer)
        {
            if (untypedValue == null)
            {
                serializer.Serialize(writer, null);
                return;
            }
            var value = (HelpText)untypedValue;
            switch (value)
            {
                case HelpText.NumberOfResultsToPullFromListMax1000:
                    serializer.Serialize(writer, "Number of results to pull from list (Max 1000)");
                    return;
                case HelpText.TagToPullArtistsFrom:
                    serializer.Serialize(writer, "Tag to pull artists from");
                    return;
            }
            throw new Exception("Cannot marshal type HelpText");
        }

        public static readonly HelpTextConverter Singleton = new HelpTextConverter();
    }

    internal class TypeEnumConverter : JsonConverter
    {
        public override bool CanConvert(Type t) => t == typeof(TypeEnum) || t == typeof(TypeEnum?);

        public override object ReadJson(JsonReader reader, Type t, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null) return null;
            var value = serializer.Deserialize<string>(reader);
            switch (value)
            {
                case "number":
                    return TypeEnum.Number;
                case "oAuth":
                    return TypeEnum.OAuth;
                case "playlist":
                    return TypeEnum.Playlist;
                case "textbox":
                    return TypeEnum.Textbox;
            }
            throw new Exception("Cannot unmarshal type TypeEnum");
        }

        public override void WriteJson(JsonWriter writer, object untypedValue, JsonSerializer serializer)
        {
            if (untypedValue == null)
            {
                serializer.Serialize(writer, null);
                return;
            }
            var value = (TypeEnum)untypedValue;
            switch (value)
            {
                case TypeEnum.Number:
                    serializer.Serialize(writer, "number");
                    return;
                case TypeEnum.OAuth:
                    serializer.Serialize(writer, "oAuth");
                    return;
                case TypeEnum.Playlist:
                    serializer.Serialize(writer, "playlist");
                    return;
                case TypeEnum.Textbox:
                    serializer.Serialize(writer, "textbox");
                    return;
            }
            throw new Exception("Cannot marshal type TypeEnum");
        }

        public static readonly TypeEnumConverter Singleton = new TypeEnumConverter();
    }

    internal class ValueConverter : JsonConverter
    {
        public override bool CanConvert(Type t) => t == typeof(Value) || t == typeof(Value?);

        public override object ReadJson(JsonReader reader, Type t, object existingValue, JsonSerializer serializer)
        {
            switch (reader.TokenType)
            {
                case JsonToken.Integer:
                    var integerValue = serializer.Deserialize<long>(reader);
                    return new Value { Integer = integerValue };
                case JsonToken.String:
                case JsonToken.Date:
                    var stringValue = serializer.Deserialize<string>(reader);
                    return new Value { String = stringValue };
                case JsonToken.StartArray:
                    var arrayValue = serializer.Deserialize<List<string>>(reader);
                    return new Value { StringArray = arrayValue };
            }
            throw new Exception("Cannot unmarshal type Value");
        }

        public override void WriteJson(JsonWriter writer, object untypedValue, JsonSerializer serializer)
        {
            var value = (Value)untypedValue;
            if (value.Integer != null)
            {
                serializer.Serialize(writer, value.Integer.Value);
                return;
            }
            if (value.String != null)
            {
                serializer.Serialize(writer, value.String);
                return;
            }
            if (value.StringArray != null)
            {
                serializer.Serialize(writer, value.StringArray);
                return;
            }
            throw new Exception("Cannot marshal type Value");
        }

        public static readonly ValueConverter Singleton = new ValueConverter();
    }

    internal class ImplementationConverter : JsonConverter
    {
        public override bool CanConvert(Type t) => t == typeof(Implementation) || t == typeof(Implementation?);

        public override object ReadJson(JsonReader reader, Type t, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null) return null;
            var value = serializer.Deserialize<string>(reader);
            switch (value)
            {
                case "LastFmTag":
                    return Implementation.LastFmTag;
                case "SpotifyFollowedArtists":
                    return Implementation.SpotifyFollowedArtists;
                case "SpotifyPlaylist":
                    return Implementation.SpotifyPlaylist;
                case "SpotifySavedAlbums":
                    return Implementation.SpotifySavedAlbums;
            }
            throw new Exception("Cannot unmarshal type Implementation");
        }

        public override void WriteJson(JsonWriter writer, object untypedValue, JsonSerializer serializer)
        {
            if (untypedValue == null)
            {
                serializer.Serialize(writer, null);
                return;
            }
            var value = (Implementation)untypedValue;
            switch (value)
            {
                case Implementation.LastFmTag:
                    serializer.Serialize(writer, "LastFmTag");
                    return;
                case Implementation.SpotifyFollowedArtists:
                    serializer.Serialize(writer, "SpotifyFollowedArtists");
                    return;
                case Implementation.SpotifyPlaylist:
                    serializer.Serialize(writer, "SpotifyPlaylist");
                    return;
                case Implementation.SpotifySavedAlbums:
                    serializer.Serialize(writer, "SpotifySavedAlbums");
                    return;
            }
            throw new Exception("Cannot marshal type Implementation");
        }

        public static readonly ImplementationConverter Singleton = new ImplementationConverter();
    }

    internal class ImplementationNameConverter : JsonConverter
    {
        public override bool CanConvert(Type t) => t == typeof(ImplementationName) || t == typeof(ImplementationName?);

        public override object ReadJson(JsonReader reader, Type t, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null) return null;
            var value = serializer.Deserialize<string>(reader);
            switch (value)
            {
                case "Last.fm Tag":
                    return ImplementationName.LastFmTag;
                case "Spotify Followed Artists":
                    return ImplementationName.SpotifyFollowedArtists;
                case "Spotify Playlists":
                    return ImplementationName.SpotifyPlaylists;
                case "Spotify Saved Albums":
                    return ImplementationName.SpotifySavedAlbums;
            }
            throw new Exception("Cannot unmarshal type ImplementationName");
        }

        public override void WriteJson(JsonWriter writer, object untypedValue, JsonSerializer serializer)
        {
            if (untypedValue == null)
            {
                serializer.Serialize(writer, null);
                return;
            }
            var value = (ImplementationName)untypedValue;
            switch (value)
            {
                case ImplementationName.LastFmTag:
                    serializer.Serialize(writer, "Last.fm Tag");
                    return;
                case ImplementationName.SpotifyFollowedArtists:
                    serializer.Serialize(writer, "Spotify Followed Artists");
                    return;
                case ImplementationName.SpotifyPlaylists:
                    serializer.Serialize(writer, "Spotify Playlists");
                    return;
                case ImplementationName.SpotifySavedAlbums:
                    serializer.Serialize(writer, "Spotify Saved Albums");
                    return;
            }
            throw new Exception("Cannot marshal type ImplementationName");
        }

        public static readonly ImplementationNameConverter Singleton = new ImplementationNameConverter();
    }

    internal class ListTypeConverter : JsonConverter
    {
        public override bool CanConvert(Type t) => t == typeof(ListType) || t == typeof(ListType?);

        public override object ReadJson(JsonReader reader, Type t, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null) return null;
            var value = serializer.Deserialize<string>(reader);
            switch (value)
            {
                case "lastFm":
                    return ListType.LastFm;
                case "spotify":
                    return ListType.Spotify;
            }
            throw new Exception("Cannot unmarshal type ListType");
        }

        public override void WriteJson(JsonWriter writer, object untypedValue, JsonSerializer serializer)
        {
            if (untypedValue == null)
            {
                serializer.Serialize(writer, null);
                return;
            }
            var value = (ListType)untypedValue;
            switch (value)
            {
                case ListType.LastFm:
                    serializer.Serialize(writer, "lastFm");
                    return;
                case ListType.Spotify:
                    serializer.Serialize(writer, "spotify");
                    return;
            }
            throw new Exception("Cannot marshal type ListType");
        }

        public static readonly ListTypeConverter Singleton = new ListTypeConverter();
    }

    internal class MonitorNewItemsConverter : JsonConverter
    {
        public override bool CanConvert(Type t) => t == typeof(MonitorNewItems) || t == typeof(MonitorNewItems?);

        public override object ReadJson(JsonReader reader, Type t, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null) return null;
            var value = serializer.Deserialize<string>(reader);
            if (value == "all")
            {
                return MonitorNewItems.All;
            }
            throw new Exception("Cannot unmarshal type MonitorNewItems");
        }

        public override void WriteJson(JsonWriter writer, object untypedValue, JsonSerializer serializer)
        {
            if (untypedValue == null)
            {
                serializer.Serialize(writer, null);
                return;
            }
            var value = (MonitorNewItems)untypedValue;
            if (value == MonitorNewItems.All)
            {
                serializer.Serialize(writer, "all");
                return;
            }
            throw new Exception("Cannot marshal type MonitorNewItems");
        }

        public static readonly MonitorNewItemsConverter Singleton = new MonitorNewItemsConverter();
    }

    internal class RootFolderPathConverter : JsonConverter
    {
        public override bool CanConvert(Type t) => t == typeof(RootFolderPath) || t == typeof(RootFolderPath?);

        public override object ReadJson(JsonReader reader, Type t, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null) return null;
            var value = serializer.Deserialize<string>(reader);
            switch (value)
            {
                case "/music":
                    return RootFolderPath.RootFolderPathMusic;
                case "/music/":
                    return RootFolderPath.Music;
            }
            throw new Exception("Cannot unmarshal type RootFolderPath");
        }

        public override void WriteJson(JsonWriter writer, object untypedValue, JsonSerializer serializer)
        {
            if (untypedValue == null)
            {
                serializer.Serialize(writer, null);
                return;
            }
            var value = (RootFolderPath)untypedValue;
            switch (value)
            {
                case RootFolderPath.RootFolderPathMusic:
                    serializer.Serialize(writer, "/music");
                    return;
                case RootFolderPath.Music:
                    serializer.Serialize(writer, "/music/");
                    return;
            }
            throw new Exception("Cannot marshal type RootFolderPath");
        }

        public static readonly RootFolderPathConverter Singleton = new RootFolderPathConverter();
    }

    internal class ShouldMonitorConverter : JsonConverter
    {
        public override bool CanConvert(Type t) => t == typeof(ShouldMonitor) || t == typeof(ShouldMonitor?);

        public override object ReadJson(JsonReader reader, Type t, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null) return null;
            var value = serializer.Deserialize<string>(reader);
            if (value == "entireArtist")
            {
                return ShouldMonitor.EntireArtist;
            }
            throw new Exception("Cannot unmarshal type ShouldMonitor");
        }

        public override void WriteJson(JsonWriter writer, object untypedValue, JsonSerializer serializer)
        {
            if (untypedValue == null)
            {
                serializer.Serialize(writer, null);
                return;
            }
            var value = (ShouldMonitor)untypedValue;
            if (value == ShouldMonitor.EntireArtist)
            {
                serializer.Serialize(writer, "entireArtist");
                return;
            }
            throw new Exception("Cannot marshal type ShouldMonitor");
        }

        public static readonly ShouldMonitorConverter Singleton = new ShouldMonitorConverter();
    }
}
