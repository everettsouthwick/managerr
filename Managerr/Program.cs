using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using RadarrSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Managerr.Services;

namespace Managerr
{
    internal class Program
    {
        private static async Task Main(string[] args)
        {
            IConfiguration Configuration = new ConfigurationBuilder()
              .SetBasePath(Directory.GetCurrentDirectory())
              .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
              .AddEnvironmentVariables()
              .Build();

            await SQLService.Initalize();

            var primary = RadarrService.BuildRadarrClient(Configuration.GetSection("primaryRadarr"));
            var secondary = RadarrService.BuildRadarrClient(Configuration.GetSection("secondaryRadarr"));

            await RadarrService.GetOldMissingMovies(primary);
            //await RadarrService.NetImportSync(primary);
            await RadarrService.MovieSync(primary, secondary);
            await RadarrService.MissingMoviesSearch(primary);
            await RadarrService.MissingMoviesSearch(secondary);
            await RadarrService.CutOffUnmetMoviesSearch(primary);
            await RadarrService.CutOffUnmetMoviesSearch(secondary);
        }
    }
}