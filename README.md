# SpotifyPlexSync

Simple tool to sync some Spotify playlists to your Plex server.

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
        "playlistid|playlistdescription (only used for this json, not used as title)"
    ],
    "Prefix": "Spotify - :will used as prefix for the synced playlist",
    "LogUnmatched": "true|false: logs unmatched tracks to unmatched_yyyy-MM-dd.log"
}
```
