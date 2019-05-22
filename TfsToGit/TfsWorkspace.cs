using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace TfsToGit
{
    internal class TfsWorkspace : IDisposable
    {
        public TfsWorkspace(Uri tfsCollectionName, string serverFolder)
        {
            TfsCollectionName = tfsCollectionName;
            ServerFolder = serverFolder;

            //var id = $"{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("n").Substring(0, 5)}";
            var id = Hash($"{tfsCollectionName}|{serverFolder}").Substring(0, 10);
            WorkspaceName = CreateUniqueWorkspaceName(id);
            LocalFolder = CreateLocalFolder(id);
            CreateWorkspace(WorkspaceName, tfsCollectionName, serverFolder, LocalFolder);
        }

        public Uri TfsCollectionName { get; }
        public string ServerFolder { get; }
        public DirectoryInfo LocalFolder { get; }
        private string WorkspaceName { get; }

        public void GetSpecificVersion(int changesetId)
        {
            Console.WriteLine($"Getting changeset: {changesetId}");
            const int maxRetries = 4;
            int attemptCount = 0;
            bool success;
            Exception exception = null;

            do
            {
                attemptCount++;

                try
                {
                    Command.ExecuteTfCommand($"get \"{LocalFolder.FullName}\" /recursive /version:{changesetId}");
                    return;
                }
                catch (Exception ex)
                {
                    exception = ex;
                    success = false;
                    var delay = TimeSpan.FromSeconds(35);
                    Console.WriteLine($"Retrying attempt #{attemptCount} in {delay.TotalSeconds} seconds...");
                    Thread.Sleep(delay);
                }
                finally
                {
                    EnsureLocalFolder();
                }
            } while (!success && attemptCount < maxRetries);

            throw exception;
        }

        private void EnsureLocalFolder()
        {
            LocalFolder.Refresh();
            if (LocalFolder.Exists) return;

            LocalFolder.Create();
        }

        private static string CreateUniqueWorkspaceName(string id)
        {
            return $"{Environment.MachineName}-{id}";
        }

        private static DirectoryInfo CreateLocalFolder(string id)
        {
            var basePath = Path.GetTempPath();
            var localFolder = new DirectoryInfo(Path.Combine(basePath, "t2g", id));
            localFolder.Create();
            return localFolder;
        }

        private static void CreateWorkspace(string workspaceName, Uri tfsCollectionName, string serverFolder, DirectoryInfo localFolder)
        {
            DeleteWorkspace(workspaceName, tfsCollectionName);

            Console.WriteLine($"Creating workspace '{workspaceName}' for {tfsCollectionName.AbsoluteUri}");
            Command.ExecuteTfCommand($"workspace /new /noprompt /location:server /permission:Private /collection:\"{tfsCollectionName.AbsoluteUri}\" /comment:\"TFVC to Git migration workspace\" \"{workspaceName}\"", localFolder.FullName);

            Command.ExecuteTfCommand($"workfold /unmap /collection:\"{tfsCollectionName.AbsoluteUri}\" /workspace:\"{workspaceName}\" \"$/\"");
            MapWorkspace(workspaceName, tfsCollectionName, serverFolder, localFolder);
        }

        private static void MapWorkspace(string workspaceName, Uri tfsCollectionName, string serverFolder, DirectoryInfo localFolder)
        {
            Console.WriteLine($"Mapping workspace '{workspaceName}' {serverFolder} => {localFolder.FullName}");
            Command.ExecuteTfCommand($"workfold /collection:\"{tfsCollectionName.AbsoluteUri}\" /workspace:\"{workspaceName}\" /map \"{serverFolder}\" \"{localFolder.FullName}\"");

            // Cloak nuget packages
            Console.WriteLine($"Cloaking packages workspace '{workspaceName}' {serverFolder}/packages");
            Command.ExecuteTfCommand($"workfold /cloak \"{serverFolder}/packages\" /collection:\"{tfsCollectionName.AbsoluteUri}\" /workspace:\"{workspaceName}\"");
        }

        private static void DeleteWorkspace(string workspaceName, Uri tfsCollectionName)
        {
            Console.WriteLine($"Deleting workspace: {workspaceName}");
            Command.ExecuteTfCommand($"workspace /delete /noprompt /collection:\"{tfsCollectionName.AbsoluteUri}\" \"{workspaceName}\"");
        }

        private static string Hash(string input)
        {
            using (var sha1 = new SHA1Managed())
            {
                var hash = sha1.ComputeHash(Encoding.UTF8.GetBytes(input));
                var sb = new StringBuilder(hash.Length * 2);

                foreach (byte b in hash)
                {
                    sb.Append(b.ToString("X2"));
                }

                return sb.ToString();
            }
        }

        public void Dispose()
        {
            try
            {
                DeleteWorkspace(WorkspaceName, TfsCollectionName);
            }
            catch
            {
                Console.Error.WriteLine($"Failed to delete workspace: {WorkspaceName}");
            }

            try
            {
                LocalFolder.Delete(true);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to delete local folder: {LocalFolder}.  {ex.Message}");
            }
        }
    }
}
