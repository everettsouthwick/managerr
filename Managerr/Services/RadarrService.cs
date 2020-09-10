using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using RadarrSharp;
using RadarrSharp.Models;
using System.Linq;

namespace Managerr.Services
{
    public static class RadarrService
    {
        public static RadarrClient BuildRadarrClient(IConfiguration config)
        {
            return new RadarrClient(config.GetValue<string>("host"), config.GetValue<int>("port"), config.GetValue<string>("apiKey"));
        }

        public static async Task CutOffUnmetMoviesSearch(RadarrClient radarr)
        {
            Console.WriteLine($"Searching for cutoff unmet movies.");
            await radarr.Command.CutOffUnmetMoviesSearch("monitored", "true");
        }

        public static async Task GetOldMissingMovies(RadarrClient radarr)
        {
            var movies = await radarr.Movie.GetMovies();
            var oldMovies = movies.Where(m => m.Monitored == true && m.Downloaded == false && m.Status == RadarrSharp.Enums.Status.Released);
            foreach (var movie in oldMovies)
            {
                // Log that this movie is available, but has yet to be downloaded.
                await SQLService.AddOrUpdateMovie(movie);

                // If this is past the 5th of the month, we want to unmonitor all the old movies.
                if (DateTime.Now.Day > 5)
                {
                    var failCount = await SQLService.GetFailCount(movie);
                    if (failCount >= 30)
                    {
                        Console.WriteLine($"Deleting {movie.Title} for failing more than 30 times.");
                        await radarr.Movie.DeleteMovie(movie.Id, true);
                    }
                    else if (failCount >= 7)
                    {
                        Console.WriteLine($"Unmonitoring {movie.Title} for failing more than 7 times.");
                        movie.Monitored = false;
                        await radarr.Movie.UpdateMovie(movie);
                    }
                }
            }

            // If it is before the 5th of the month, we want to once again monitor all the new movies.
            if (DateTime.Now.Day < 5)
            {
                oldMovies = movies.Where(m => m.Monitored == false && m.Downloaded == false && m.Status == RadarrSharp.Enums.Status.Released);
                foreach (var movie in oldMovies)
                {
                    Console.WriteLine($"Monitoring all old movies.");
                    movie.Monitored = true;
                    movie.MinimumAvailability = RadarrSharp.Enums.MinimumAvailability.Released;
                    await radarr.Movie.UpdateMovie(movie);
                }
            }
        }

        public static async Task MissingMoviesSearch(RadarrClient radarr)
        {
            Console.WriteLine($"Searching for missing movies.");
            await radarr.Command.MissingMoviesSearch("status", "released");
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

        private static IList<Movie> CalculateDifference(IList<Movie> primaryMovies, IList<Movie> secondaryMovies)
        {
            var difference = new List<Movie>();

            foreach (var primaryMovie in primaryMovies)
            {
                var movie = secondaryMovies.SingleOrDefault(m => m.TmdbId == primaryMovie.TmdbId);
                if (movie == null)
                {
                    difference.Add(movie);
                }
            }

            return difference;
        }

        private static async Task<int> GetQualityProfileId(RadarrClient radarr)
        {
            return (await radarr.QualityDefinition.GetQualityDefinitions())[0].Id;
        }

        private static async Task<string> GetRootFolderPath(RadarrClient radarr)
        {
            return (await radarr.RootFolder.GetRootFolders())[0].Path;
        }
    }
}