# managerr
![PRs Welcome](https://img.shields.io/badge/PRs-welcome-brightscreen.svg)

managerr syncs content libraries of multiple Radarr and Sonarr instances, in addition to backlog searching, and maintenance of libraries.

## Dependencies

- .NET Core 3+
- [RadarrSharp](https://github.com/everettsouthwick/RadarrSharp)
- [SonarrSharp](https://github.com/Hertizch/SonarrSharp)

## Getting Started

1. Clone or download the project
2. Install dependencies with `npm install` in the root directory
3. Execute `node server`

## Features

- Syncronizes the media libraries of multiple Radarr instances
- Performs daily backlog searches of missing and cutoff unmet media
- Unmonitors missing movies that continously fail to be found

## Acknowledgements

- [Radarr](https://github.com/Radarr/Radarr)
- [Sonarr](https://github.com/Sonarr/Sonarr)

## TODO

- Implement Sonarr sync and management
- Cleanup code
- Test suite
