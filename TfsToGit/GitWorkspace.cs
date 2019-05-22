using LibGit2Sharp;
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace TfsToGit
{
    internal class GitWorkspace
    {
        public GitWorkspace(DirectoryInfo repositoryHome, string serverHomePath)
            : this(repositoryHome, repositoryHome, serverHomePath)
        {
        }

        private GitWorkspace(DirectoryInfo repositoryHome, DirectoryInfo workingDirectory, string serverHomePath)
        {
            if (string.IsNullOrWhiteSpace("serverHomePath")) throw new ArgumentException("Server home path can't be EMPTY");

            RepositoryHome = repositoryHome ?? throw new ArgumentNullException("repositoryHome");
            WorkingDirectory = workingDirectory ?? throw new ArgumentNullException("workingDirectory");
            ServerHomePath = serverHomePath.TrimEnd('/');

            Repository = Repository.IsValid(repositoryHome.FullName) ? new Repository(repositoryHome.FullName) : CreateRepository(repositoryHome);
        }

        public DirectoryInfo RepositoryHome { get; }
        public DirectoryInfo WorkingDirectory { get; }
        public string ServerHomePath { get; }
        public Repository Repository { get; }

        public string MapPath(string serverPath)
        {
            if (!MapPath(serverPath, out string localPath)) throw new DirectoryNotFoundException(serverPath);

            return localPath;
        }

        public bool MapPath(string serverPath, out string localPath)
        {
            localPath = null;

            if (!serverPath.StartsWith(ServerHomePath, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var relativePathFromServerPath = serverPath.Substring(ServerHomePath.Length);
            while (Path.IsPathRooted(relativePathFromServerPath))
            {
                relativePathFromServerPath = relativePathFromServerPath.Substring(1);
            }

            localPath = Path.GetFullPath(Path.Combine(WorkingDirectory.FullName, relativePathFromServerPath));
            return true;
        }

        public void Commit(string message, Signature author, Signature commiter)
        {
            CommitWithGitLib(message, author, commiter);
            //CommitWithGitExecutable(message, author, commiter);
        }

        private static string EscapeCommandLineArg(string s)
        {
            var escaped = "\"" + Regex.Replace(s, @"(\\+)$", @"$1$1") + "\"";
            return escaped;
        }

        private void CommitWithGitExecutable(string message, Signature author, Signature commiter)
        {
            Command.ExecuteGitCommand($"add .", RepositoryHome.FullName);

            Command.ExecuteGitCommand($"commit -m {EscapeCommandLineArg(message)}", RepositoryHome.FullName, processStartInfo =>
            {
                const string gitDateFormat = "ddd MMM d HH:mm:ss yyyy zzz";
                processStartInfo.EnvironmentVariables["GIT_COMMITTER_NAME"] = commiter.Name;
                processStartInfo.EnvironmentVariables["GIT_COMMITTER_EMAIL"] = commiter.Email;
                processStartInfo.EnvironmentVariables["GIT_COMMITTER_DATE"] = commiter.When.ToString(gitDateFormat);
                processStartInfo.EnvironmentVariables["GIT_AUTHOR_NAME"] = author.Name;
                processStartInfo.EnvironmentVariables["GIT_AUTHOR_EMAIL"] = author.Email;
                processStartInfo.EnvironmentVariables["GIT_AUTHOR_DATE"] = author.When.ToString(gitDateFormat);
            });
        }

        private void CommitWithGitLib(string message, Signature author, Signature commiter)
        {
            var filesToStage = (from status in Repository.RetrieveStatus()
                                where status.State != FileStatus.Ignored
                                select status.FilePath).ToList();

            if (!filesToStage.Any()) return;

            Commands.Stage(Repository, filesToStage);

            try
            {
                Repository.Commit(message, author, commiter);
            }
            catch (EmptyCommitException)
            {
                Console.Error.WriteLine($"   [EMPTY COMMIT] {message}");
            }
        }

        public void Tag(string tagName, Signature tagger, string message)
        {
            Repository.ApplyTag(tagName, tagger, message);
        }

        private static Repository CreateRepository(DirectoryInfo repositoryHome)
        {
            if (!Directory.Exists(repositoryHome.FullName))
            {
                Directory.CreateDirectory(repositoryHome.FullName);
            }

            Repository.Init(repositoryHome.FullName);
            return new Repository(repositoryHome.FullName);
        }
    }
}
