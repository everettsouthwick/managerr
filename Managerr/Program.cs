using Microsoft.Extensions.Configuration;
using RadarrSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Managerr
{
    internal class Program
    {
        private static async Task<IList<RadarrSharp.Models.Movie>> CalculateDifference(IList<RadarrSharp.Models.Movie> primary, IList<RadarrSharp.Models.Movie> secondary)
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

        private static async Task Main(string[] args)
        {
            IConfiguration Configuration = new ConfigurationBuilder()
              .SetBasePath(Directory.GetCurrentDirectory())
              .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
              .AddEnvironmentVariables()
              .Build();

            var primary = new RadarrClient(Configuration.GetValue<string>("primaryRadarr:host"), Configuration.GetValue<int>("primaryRadarr:port"), Configuration.GetValue<string>("primaryRadarr:apikey"));
            var secondary = new RadarrClient(Configuration.GetValue<string>("secondaryRadarr:host"), 80, Configuration.GetValue<string>("secondaryRadarr:apikey"));
            secondary.GetType().GetProperty("ApiUrl").SetValue(secondary, $"{secondary.Host}/api", null);

            await SyncMovies(primary, secondary);
        }

        private static async Task SyncMovies(RadarrClient primary, RadarrClient secondary)
        {
            var test = await primary.RootFolder.GetRootFolders();
            foreach (var rootFolder in test)
            {
                Console.WriteLine(rootFolder.Path);
            }
            ////var primaryMovies = await primary.Movie.GetMovies();
            //var primaryMovies = primary.Movie.GetMoviesPaged();
            ////var secondaryMovies = await secondary.Movie.GetMovies();

            ////var difference = await CalculateDifference(primaryMovies, secondaryMovies);

            //Console.WriteLine((await primary.RootFolder.GetRootFolders())[0].Path);

            ////foreach (var movie in difference)
            ////{
            ////    //movie.Path = movie.Path.Replace(, "4k");
            ////}
        }
    }
}