using LibGit2Sharp;
using Microsoft.TeamFoundation.Client;
using Microsoft.TeamFoundation.Framework.Client;
using Microsoft.TeamFoundation.Framework.Common;
using Microsoft.TeamFoundation.VersionControl.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Caching;

namespace TfsToGit
{
    internal class RepositoryMigrator
    {
        public static void Migrate(TfsTeamProjectCollection teamProjectCollection, IEnumerable<Func<GitWorkspace>> workspaces, MigrationSettings settings)
        {
            foreach (var workspaceFactory in workspaces)
            {
                var workspace = workspaceFactory();

                try
                {
                    Console.WriteLine(teamProjectCollection.Uri);
                    Console.WriteLine($"{ workspace.ServerHomePath} => {workspace.RepositoryHome}");
                    Console.WriteLine("-------------------------------------");

                    Migrate(teamProjectCollection, workspace);
                    workspace.Tag($"migration/{DateTime.UtcNow.ToString("yyyyMMddTHHmmss")}z", new Signature("Build Management", "Build.Management@mrcooper.com", DateTimeOffset.Now), $"{teamProjectCollection.Uri}\n{workspace.ServerHomePath} => {workspace.RepositoryHome}");


                    Console.WriteLine($"COMPLETED {workspace.RepositoryHome}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"ERROR migrating {workspace.RepositoryHome}");
                    Console.WriteLine(ex.Message);

                    if (!settings.ContinueOnError) throw;
                }
            }
        }

        private static void Migrate(TfsTeamProjectCollection teamProjectCollection, GitWorkspace workspace)
        {
            teamProjectCollection.EnsureAuthenticated();

            var versionControlServer = teamProjectCollection.GetService<VersionControlServer>();
            var history = (from changeset in versionControlServer.QueryHistory(workspace.ServerHomePath, RecursionType.Full)
                           select versionControlServer.GetChangeset(changeset.ChangesetId))
                           .OrderBy(changeset => changeset.ChangesetId);

            using (var tfsWorkspace = new TfsWorkspace(teamProjectCollection.Uri, workspace.ServerHomePath))
            {
                ProcessChangesets(teamProjectCollection, workspace, tfsWorkspace, history);
            }
        }

        private static void ProcessChangesets(TfsTeamProjectCollection teamProjectCollection, GitWorkspace workspace, TfsWorkspace tfsWorkspace, IOrderedEnumerable<Changeset> history)
        {
            foreach (Changeset changeset in history)
            {
                Console.WriteLine("Log Time: " + DateTimeOffset.Now.ToString());
                Console.WriteLine("Changeset Id: " + changeset.ChangesetId);
                Console.WriteLine("Owner: " + changeset.Owner);
                Console.WriteLine("Date: " + changeset.CreationDate.ToString());
                Console.WriteLine("Comment: " + changeset.Comment);

                tfsWorkspace.GetSpecificVersion(changeset.ChangesetId);
                Command.ExecuteRobocopyCommand($@"/MIR ""{tfsWorkspace.LocalFolder.FullName}"" ""{workspace.RepositoryHome.FullName}"" /XD "".git"" ""$tf"" ""packages""");

                var author = CreateSignature(teamProjectCollection, changeset.Committer, changeset.CommitterDisplayName, changeset.CreationDate);
                workspace.Commit(changeset.Comment, author, author);
            }
        }

        private static Signature CreateSignature(TfsTeamProjectCollection teamProjectCollection, string username, string defaultName, DateTime when)
        {
            var user = GetTfsUser(teamProjectCollection, username);
            var userName = !string.IsNullOrWhiteSpace(user?.Name) ? user.Name : defaultName;
            var userEmail = !string.IsNullOrWhiteSpace(user?.Email) ? user.Email : "unknown";
            return new Signature(userName, userEmail, new DateTimeOffset(when));
        }

        private static void WriteError(string message)
        {
            try
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Error.WriteLine(message);
            }
            finally
            {
                Console.ResetColor();
            }
        }

        private static UserInfo GetTfsUser(TfsTeamProjectCollection teamProjectCollection, string username)
        {
            var user = MemoryCache.Default.Get(username) as UserInfo;
            if (user != null) return user;

            try
            {
                var identityManagementService = teamProjectCollection.GetService<IIdentityManagementService2>();
                var validIdentities = identityManagementService.ReadIdentities(IdentitySearchFactor.AccountName, new[] { "Project Collection Valid Users" }, MembershipQuery.Expanded, ReadIdentityOptions.None)[0][0].Members;
                var identities = identityManagementService.ReadIdentities(validIdentities, MembershipQuery.None, ReadIdentityOptions.None).Where(x => !x.IsContainer);
                user = (from identity in identities
                        where StringComparer.OrdinalIgnoreCase.Equals(identity.UniqueName, username)
                        let email = identity.GetProperty("Mail") as string
                        select new UserInfo { Name = identity.DisplayName, Email = email }).FirstOrDefault()
                        ?? new UserInfo();

                MemoryCache.Default.Add(username, user, DateTimeOffset.UtcNow.AddDays(7));
            }
            catch (Exception ex)
            {
                WriteError(ex.Message);
            }

            return user;
        }
    }
}
