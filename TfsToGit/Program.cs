using Microsoft.TeamFoundation.Client;
using System;
using System.IO;
using System.Text;

namespace TfsToGit
{
    internal class Program
    {
        private static int Main(string[] args)
        {
            Options.InvokeWithOptions(args, MigrateRepository);
            return 0;
        }

        private static void MigrateRepository(Options options)
        {
            var workspaces = new[]
            {
                new Func<GitWorkspace>(() => new GitWorkspace(new DirectoryInfo(options.RepositoryDirectory), options.TfsBranchPath))
            };

            var teamProjectCollection = TfsTeamProjectCollectionFactory.GetTeamProjectCollection(new Uri(options.TeamProjectCollectionUri));
            var settings = new MigrationSettings
            {
                ContinueOnError = options.ContinueOnError,
            };

            RepositoryMigrator.Migrate(teamProjectCollection, workspaces, settings);
        }

        private static string GetPassword()
        {
            Console.WriteLine("Enter password:");

            ConsoleKeyInfo key;
            var pwd = new StringBuilder();

            while ((key = Console.ReadKey(true)).Key != ConsoleKey.Enter)
            {
                if (key.Key != ConsoleKey.Backspace)
                {
                    pwd.Append(key.KeyChar);
                    Console.Write("*");
                }
                else
                {
                    if (key.Key == ConsoleKey.Backspace && pwd.Length > 0)
                    {
                        pwd.Remove(pwd.Length - 1, 1);
                        Console.Write("\b \b");
                    }
                }
            }

            return pwd.ToString();
        }
    }
}


