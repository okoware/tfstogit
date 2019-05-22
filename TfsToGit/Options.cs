using CommandLine;
using CommandLine.Text;
using System;
using System.Collections.Generic;
using System.Linq;

namespace TfsToGit
{
    internal class Options
    {
        [Option('p', "teamprojectcollection", HelpText = "The TFS Team Project URI (e.g. https://tfsserver/tfs/AcmeCorp)", Required = true)]
        public string TeamProjectCollectionUri { get; set; }

        [Option('b', "tfsbranchpath", HelpText = "The TFS path to the directory to clone (e.g. $/Acme/AccountingWebsite/trunk)", Required = true)]
        public string TfsBranchPath { get; set; }

        [Option('r', "repository", HelpText = @"The repository directory (e.g. C:\repos\migration\AccountingWebsite", Required = true)]
        public string RepositoryDirectory { get; set; }

        [Option("continueonerror", HelpText = "Set to true to continue on error and false to hault on error", Required = false, Default = false)]
        public bool ContinueOnError { get; set; }

        [Usage]
        public static IEnumerable<Example> Examples
        {
            get
            {
                return new[]
                {
                    new Example("Migrate TFS TFVC repository to a Git repository", new Options
                    {
                        TeamProjectCollectionUri = "https://tfsserver/tfs/AcmeCorp",
                        TfsBranchPath = "$/Acme/AccountingWebsite/trunk",
                        RepositoryDirectory = @"C:\repos\migration\AccountingWebsite"
                    })
                };
            }
        }

        public static void InvokeWithOptions(string[] args, Action<Options> run)
        {
            Parser.Default
                .ParseArguments<Options>(args)
                .WithParsed(run)
                .WithNotParsed(errors =>
                {
                    var isInformational = errors.All(e => e.Tag == ErrorType.HelpRequestedError || e.Tag == ErrorType.VersionRequestedError);
                    Environment.Exit(isInformational ? 0 : 1);
                });

        }
    }
}
