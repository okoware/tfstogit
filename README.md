# TFS to GIT
Migrates TFS TFVC repositories to Git.

## Usage
```bash
TfsToGit --tfsbranchpath $/Acme/AccountingWebsite/trunk --teamprojectcollection https://tfsserver/tfs/AcmeCorp --repository C:\repos\migration\AccountingWebsite
```

## Prerequisites
The following should be installed on your machine.  Their paths are configured in the config file.
- [Git for Windows](https://gitforwindows.org/)
- TFS TF client side tool (Comes with Visual Studio installation)

## Remarks
Getting this old code off my hard drive.  This utility has proven useful over the years in helping various people migrate from TFS TFVC to Git.  Hopefully, someone else finds it useful.