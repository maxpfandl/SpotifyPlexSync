# SpotifyPlexSync

Simple tool to sync some Spotify playlists to your Plex server.

Since Plex is only able to show Playlists by CreationDate, not ChangeDate, every change is resulting in deletion and recreation of the existing playlist. If the playlist is the same and no new tracks used by this playlist are found on Plex, no update will happen.

Needed configuration:

```json
{
    "Plex": {
        "Url": "your server url", #no tailing /
        "Token": "plextoken: pls google",
        "ServerId": "obtain from [serverurl]/identity"
    },
    "Spotify": {
        "ClientID": "spotify client id: you can create an app here https://developer.spotify.com/dashboard/applications",
        "ClientSecret": "spotify secret"
    },
    "Lidarr": {
        "Url":"lidarrurl", #no tailing /
        "ApiKey": "apikey"
    },
    "Sync": [
        "playlistid|playlistdescription (only used for this json, not used as title)",
        "playlistid2|playlistdescription (only used for this json, not used as title)"
    ],
    "Prefix": "Spotify - :will used as prefix for the synced playlist",
    "LogUnmatched": "true|false: logs unmatched tracks to unmatched_yyyy-MM-dd.log",
    "AddAuthorToTitle": "true|false: add author to title of the playlist like 'myplaylist by author'",
    "MaxTracks": 150, #set 0 to ignore
    "CheckSpotifySnapshot": "true|false: only update Playlists with updated snapshotid from spotify",
    "AddReportToDescription": "true|false: add a report to the playlistdescription in Plex (ie how many tracks have been found in Pley)",
    "WebHook": "posts json with errors/statusmessages to this webhook", 
    "WebHookBasicAuth":"user:password for webhook if needed",
}
```

## Features

### Scripted run

`SpotifyPlexSync`

Configured playlists are synced

### Specific playlist

`SpotifyPlexSync [playlistid]`

Only one specific playlist is synced

### Only new playlists

`SpotifyPlexSync new`

Syncs only the new playlists from the config, which are not yet existing in Plex

### Imported Lidarr playlists

`SpotifyPlexSync lidarr`

Connects to lidarr and syncs all playlists in the Importlist calles "Spotify Playlists".
Needs Lidarr configured in appsettings

### Imported new Lidarr playlists

`SpotifyPlexSync lidarrnew`

Connects to lidarr and syncs all playlists in the Importlist calles "Spotify Playlists" which are not yet existing in Plex.
Needs Lidarr configured in appsettings

### Full sync* 

`SpotifyPlexSync all`

Read all your playlists in your library and sync them.

### Extract*

`SpotifyPlexSync extract`

Read all your playlists in your library and show the id's and title in the console. Also a json-file called appsettings.playlists.json is written which, if located in the same folder, will be used to read the SpotifyPlaylistId's. Useful for initial configuration.


*This needs a system with a browser for authentication, so scripting is not possible AND you need to configure http://localhost:5000/callback as redirect URL in your Spotify App. Not suited for scripted usage.
