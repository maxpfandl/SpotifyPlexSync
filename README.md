# SpotifyPlexSync

Simple tool to sync some Spotify playlists to your Plex server.

Since Plex is only able to show Playlists by CreationDate, not ChangeDate, every change is resulting in deletion and recreation of the existing playlist. If the playlist is the same and no new tracks used by this playlist are found on Plex, no update will happen.

Needed configuration:

```json
{
    "Plex": {
        "Url": "your server url",
        "Token": "plextoken: pls google",
        "ServerId": "obtain from [serverurl]/identity"
    },
    "Spotify": {
        "ClientID": "spotify client id: you can create an app here https://developer.spotify.com/dashboard/applications",
        "ClientSecret": "spotify secret"
    },
    "Sync": [
        "playlistid|playlistdescription (only used for this json, not used as title)",
        "playlistid2|playlistdescription (only used for this json, not used as title)"
    ],
    "Prefix": "Spotify - :will used as prefix for the synced playlist",
    "LogUnmatched": "true|false: logs unmatched tracks to unmatched_yyyy-MM-dd.log",
    "AddAuthorToTitle": "true|false: add author to title of the playlist like 'myplaylist by author'"
}
```

## Features

### Scripted run

`SpotifyPlexSync`

Configured playlists are synced

### Specific playlist

`SpotifyPlexSync [playlistid]`

Only one specific playlist is synced

### Full sync* 

`SpotifyPlexSync all`

Read all your playlists in your library and sync them.

### Extract*

`SpotifyPlexSync extract`

Read all your playlists in your library and show the id's and title in the console. Useful for initial configuration.


*This needs a system with a browser for authentication, so scripting is not possible AND you need to configure http://localhost:5000/callback as redirect URL in your Spotify App. Not suited for scripted usage.
