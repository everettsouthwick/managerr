using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using RadarrSharp;
using RadarrSharp.Models;
using RadarrSharp.Enums;
using System.Linq;

namespace Managerr.Services
{
    public static class RadarrService
    {
        public static RadarrClient BuildRadarrClient(IConfiguration config)
        {
            Console.WriteLine($"Initializing new RadarrClient at http://{config.GetValue<string>("host")}:{config.GetValue<int>("port")}/?apiKey={config.GetValue<string>("apiKey")}");
            return new RadarrClient(config.GetValue<string>("host"), config.GetValue<int>("port"), config.GetValue<string>("apiKey"));
        }

        public static async Task CutOffUnmetMoviesSearch(RadarrClient radarr)
        {
            Console.WriteLine($"Searching for cutoff unmet movies.");
            await radarr.Command.CutOffUnmetMoviesSearch("monitored", "true");
        }

        public static async Task MissingMoviesSearch(RadarrClient radarr)
        {
            Console.WriteLine($"Searching for missing movies.");
            await radarr.Command.MissingMoviesSearch("monitored", "true");
        }

        public static async Task MovieSync(RadarrClient primary, RadarrClient secondary)
        {
            var primaryMovies = await primary.Movie.GetMovies();
            var secondaryMovies = await secondary.Movie.GetMovies();

            var difference = CalculateDifference(primaryMovies, secondaryMovies);

            // We want to add all the movies the primary Radarr has to the secondary Radarr client.
            foreach (var movie in difference)
            {
                Console.WriteLine($"Adding {movie.Title} to secondary Radarr.");
                await secondary.Movie.AddMovie(movie.Title, movie.Year, await GetQualityProfileId(secondary), movie.TitleSlug, movie.Images, Convert.ToInt32(movie.TmdbId), await GetRootFolderPath(secondary), movie.MinimumAvailability, movie.Monitored, new RadarrSharp.Endpoints.Movie.AddOptions { SearchForMovie = true });
            }

            // Next we want to find all the movies that the secondary Radarr has that the primary Radarr does not.
            primaryMovies = await primary.Movie.GetMovies();
            secondaryMovies = await secondary.Movie.GetMovies();

            difference = CalculateDifference(secondaryMovies, primaryMovies);

            // We want to remove all the movies that the secondary Radarr has that the primary Radarr does not have. The primary Radarr client should always be the single source of truth.
            foreach (var movie in difference)
            {
                Console.WriteLine($"Removing {movie.Title} from secondary Radarr.");
                await secondary.Movie.DeleteMovie(movie.Id);
            }
        }

        public static async Task NetImportSync(RadarrClient radarr)
        {
            Console.WriteLine($"Doing net import sync.");
            await radarr.Command.NetImportSync();
        }

        public static async Task UpdateMonitoredStatuses(RadarrClient radarr)
        {
            // Monitor all the movies that were recently released.
            await MonitorNewlyReleasedMovies(radarr);

            // Unmonitor all the movies that are unreleased.
            await UnmonitorUnreleasedMovies(radarr);

            // After the first 3 days of the month, we want to log the released movies that have yet to be downloaded, and unmonitor them if they have failed for too long.
            if (DateTime.Now.Day > 3)
            {
                await UnmonitorExcessivelySearchedMovies(radarr);
            }
            // Otherwise, we want to monitor of the released movies that have not been downloaded.
            else
            {
                await MonitorAllReleasedMovies(radarr);
            }
        }

        private static IList<Movie> CalculateDifference(IList<Movie> primaryMovies, IList<Movie> secondaryMovies)
        {
            var difference = new List<Movie>();

            foreach (var primaryMovie in primaryMovies)
            {
                var movie = secondaryMovies.SingleOrDefault(m => m.TmdbId == primaryMovie.TmdbId);
                if (movie == null)
                {
                    difference.Add(primaryMovie);
                }
            }

            return difference;
        }

        private static async Task<int> GetQualityProfileId(RadarrClient radarr)
        {
            return (await radarr.Profile.GetProfiles())[0].Id;
        }

        private static async Task<string> GetRootFolderPath(RadarrClient radarr)
        {
            return (await radarr.RootFolder.GetRootFolders())[0].Path;
        }

        private static async Task MonitorAllReleasedMovies(RadarrClient radarr)
        {
            Console.WriteLine("Monitoring all released movies that have yet to be downloaded.");

            // Get a list of all movies that are unmonitored and released.
            var movies = (await radarr.Movie.GetMovies()).Where(m => m.Monitored == false && m.Status == Status.Released);

            foreach (var movie in movies)
            {
                Console.WriteLine($"Monitoring {movie.Title} for being unmonitored and released. This runs during the first 3 days of the month.");
                movie.Monitored = true;
                await radarr.Movie.UpdateMovie(movie);
            }
        }

        private static async Task MonitorNewlyReleasedMovies(RadarrClient radarr)
        {
            Console.WriteLine("Monitoring newly released movies.");

            // Get a list of all movies.
            var movies = await radarr.Movie.GetMovies();

            // Get a list of all paths that should be excluded.
            var paths = await SQLService.GetAllExcludedMovies();

            // Get all the unmonitored movies that are released and not in our list of excluded movies.
            var unmonitoredMovies = movies.Where(m => m.Monitored == false && m.Status == RadarrSharp.Enums.Status.Released && !paths.Contains(m.Path));

            foreach (var movie in unmonitoredMovies)
            {
                Console.WriteLine($"Monitoring {movie.Title} for being recently released.");
                movie.Monitored = true;
                await radarr.Movie.UpdateMovie(movie);
            }
        }

        private static async Task UnmonitorExcessivelySearchedMovies(RadarrClient radarr)
        {
            Console.WriteLine("Unmonitoring excessively searched movies.");

            // Get a list of all movies that are monitored, released, and not downloaded.
            var movies = (await radarr.Movie.GetMovies()).Where(m => m.Monitored == true && m.Status == Status.Released && m.Downloaded == false);

            // Get a list of all movies that are excluded.
            var excludedMovies = await SQLService.GetAllExcludedMovies();

            foreach (var movie in movies)
            {
                // Log that this movie is available, but has to be downloaded.
                await SQLService.AddOrUpdateMovie(movie);

                // If this movie has already failed 30 or more times, unmonitor it.
                if (excludedMovies.Contains(movie.Path))
                {
                    Console.WriteLine($"Unmonitoring {movie.Title} for failing to be downloaded more than 30 or more times.");
                    movie.Monitored = false;
                    await radarr.Movie.UpdateMovie(movie);
                }
            }
        }

        private static async Task UnmonitorUnreleasedMovies(RadarrClient radarr)
        {
            Console.WriteLine("Unmonitoring unreleased movies.");

            // Get a list of all movies.
            var movies = await radarr.Movie.GetMovies();

            // Get all the monitored movies that are unreleased and not downloaded.
            var unreleasedMovies = movies.Where(m => m.Monitored == true && m.Status != Status.Released && m.Downloaded == false);
            foreach (var movie in unreleasedMovies)
            {
                Console.WriteLine($"Unmonitoring {movie.Title} for being unreleased.");
                movie.Monitored = false;
                await radarr.Movie.UpdateMovie(movie);
            }
        }
    }
}