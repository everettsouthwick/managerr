using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using RadarrSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Managerr.Services;
using System.Threading;

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

            new Timer(async c => await RadarrService.MonitorNewlyReleasedMovies(primary), null, TimeSpan.Zero, TimeSpan.FromDays(1));
            Thread.Sleep(30000);

            new Timer(async c => await RadarrService.MonitorNewlyReleasedMovies(secondary), null, TimeSpan.Zero, TimeSpan.FromDays(1));
            Thread.Sleep(30000);

            new Timer(async c => await RadarrService.UnmonitorUnreleasedMovies(primary), null, TimeSpan.Zero, TimeSpan.FromDays(1));
            Thread.Sleep(30000);

            new Timer(async c => await RadarrService.UnmonitorUnreleasedMovies(secondary), null, TimeSpan.Zero, TimeSpan.FromDays(1));
            Thread.Sleep(30000);

            new Timer(async c => await RadarrService.GetOldMissingMovies(primary), null, TimeSpan.Zero, TimeSpan.FromDays(1));
            Thread.Sleep(30000);

            new Timer(async c => await RadarrService.GetOldMissingMovies(secondary), null, TimeSpan.Zero, TimeSpan.FromDays(1));
            Thread.Sleep(30000);

            new Timer(async c => await RadarrService.MovieSync(primary, secondary), null, TimeSpan.Zero, TimeSpan.FromDays(1));
            Thread.Sleep(1800000);

            new Timer(async c => await RadarrService.MissingMoviesSearch(primary), null, TimeSpan.Zero, TimeSpan.FromDays(1));
            Thread.Sleep(1800000);

            new Timer(async c => await RadarrService.MissingMoviesSearch(secondary), null, TimeSpan.Zero, TimeSpan.FromDays(1));
            Thread.Sleep(1800000);

            new Timer(async c => await RadarrService.CutOffUnmetMoviesSearch(primary), null, TimeSpan.Zero, TimeSpan.FromDays(1));
            Thread.Sleep(1800000);

            new Timer(async c => await RadarrService.CutOffUnmetMoviesSearch(secondary), null, TimeSpan.Zero, TimeSpan.FromDays(1));

            Console.ReadLine();
        }

        public static async Task RepeatActionEvery(Action action,
          TimeSpan interval, CancellationToken cancellationToken)
        {
            while (true)
            {
                action();
                Task task = Task.Delay(interval, cancellationToken);

                try
                {
                    await task;
                }
                catch (TaskCanceledException)
                {
                    return;
                }
            }
        }
    }
}