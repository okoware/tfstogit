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
    }
}


