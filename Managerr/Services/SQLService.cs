using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using RadarrSharp.Models;

namespace Managerr.Services
{
    public static class SQLService
    {
        public async static Task<List<string>> GetAllExcludedMovies()
        {
            using (var conn = new SqliteConnection("Data Source=main.db"))
            {
                var results = new List<string>();

                await conn.OpenAsync();

                var command = conn.CreateCommand();
                command.CommandText =
                    @"SELECT `Path`
                    FROM `Movies`
                    WHERE `FailCount` > $FailCount";
                command.Parameters.AddWithValue("$FailCount", 30);

                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        results.Add(reader.GetString(0));
                    }
                }

                return results;
            }
        }
        public async static Task AddOrUpdateMovie(Movie movie)
        {
            using (var conn = new SqliteConnection("Data Source=main.db"))
            {
                await conn.OpenAsync();

                var failCount = await GetFailCount(movie);

                if (failCount == 0)
                {
                    var command = conn.CreateCommand();

                    command.CommandText =
                        @"INSERT INTO `Movies` (`TmdbId`, `Path`, `FailCount`, `LastFailure`)
                        VALUES ($TmdbId, $Path, 1, $LastFailure)";
                    command.Parameters.AddWithValue("$TmdbId", movie.TmdbId);
                    command.Parameters.AddWithValue("$Path", movie.Path);
                    command.Parameters.AddWithValue("$LastFailure", DateTime.Now);

                    await command.ExecuteNonQueryAsync();
                }
                else
                {
                    var command = conn.CreateCommand();

                    command.CommandText =
                        @"UPDATE `Movies`
                        SET `FailCount` = $FailCount
                        WHERE `Path` = $Path";
                    command.Parameters.AddWithValue("$FailCount", Convert.ToInt32(failCount) + 1);
                    command.Parameters.AddWithValue("$Path", movie.Path);

                    await command.ExecuteNonQueryAsync();
                }
            }
        }

        public async static Task<int> GetFailCount(Movie movie)
        {
            using (var conn = new SqliteConnection("Data Source=main.db"))
            {
                await conn.OpenAsync();

                var command = conn.CreateCommand();
                command.CommandText =
                    @"SELECT `FailCount`
                    FROM `Movies`
                    WHERE `Path` = $Path";
                command.Parameters.AddWithValue("$Path", movie.Path);

                var failCount = await command.ExecuteScalarAsync();
                return Convert.ToInt32(failCount);
            }
        }

        public async static Task Initalize()
        {
            using (var conn = new SqliteConnection("Data Source=main.db"))
            {
                await conn.OpenAsync();

                var command = conn.CreateCommand();
                command.CommandText =
                    @"CREATE TABLE IF NOT EXISTS 'Movies' (
	                    'Id'	INTEGER NOT NULL UNIQUE,
	                    'TmdbId'	INTEGER NOT NULL,
                        'Path'      TEXT NOT NULL,
	                    'FailCount'	INTEGER,
	                    'LastFailure'	TEXT,
	                    PRIMARY KEY('Id' AUTOINCREMENT)
                    )";

                await command.ExecuteNonQueryAsync();
            }
        }
    }
}