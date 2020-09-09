using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using RadarrSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Managerr
{
    internal class Program
    {
        private static IList<RadarrSharp.Models.Movie> CalculateDifference(IList<RadarrSharp.Models.Movie> primary, IList<RadarrSharp.Models.Movie> secondary)
        {
            var difference = new List<RadarrSharp.Models.Movie>();

            foreach (var primaryMovie in primary)
            {
                var match = false;
                foreach (var secondaryMovie in secondary)
                {
                    if (primaryMovie.TmdbId == secondaryMovie.TmdbId)
                    {
                        match = true;
                        break;
                    }
                }
                if (!match)
                {
                    difference.Add(primaryMovie);
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

        private static async Task Main(string[] args)
        {
            IConfiguration Configuration = new ConfigurationBuilder()
              .SetBasePath(Directory.GetCurrentDirectory())
              .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
              .AddEnvironmentVariables()
              .Build();

            var primary = new RadarrClient(Configuration.GetValue<string>("primaryRadarr:host"), Configuration.GetValue<int>("primaryRadarr:port"), Configuration.GetValue<string>("primaryRadarr:apikey"));
            var secondary = new RadarrClient(Configuration.GetValue<string>("secondaryRadarr:host"), Configuration.GetValue<int>("secondaryRadarr:port"), Configuration.GetValue<string>("secondaryRadarr:apikey"));

            await SyncMovies(primary, secondary);
        }

        private static async Task SyncMovies(RadarrClient primary, RadarrClient secondary)
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
    }
}