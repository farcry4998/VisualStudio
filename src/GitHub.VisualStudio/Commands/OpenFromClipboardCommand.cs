﻿using System;
using System.ComponentModel.Composition;
using GitHub.Commands;
using GitHub.Services;
using GitHub.Services.Vssdk.Commands;
using Task = System.Threading.Tasks.Task;

namespace GitHub.VisualStudio.Commands
{
    [Export(typeof(IOpenFromClipboardCommand))]
    public class OpenFromClipboardCommand : VsCommand<string>, IOpenFromClipboardCommand
    {
        public const string NoGitHubUrlMessage = "Couldn't a find a GitHub URL in clipboard";
        public const string NoResolveMessage = "Couldn't resolve Git object";
        public const string NoActiveRepositoryMessage = "There is no active repository to navigate";
        public const string ChangesInWorkingDirectoryMessage = "This file has changed since the permalink was created";

        readonly Lazy<IGitHubContextService> gitHubContextService;
        readonly Lazy<ITeamExplorerContext> teamExplorerContext;
        readonly Lazy<IVSServices> vsServices;

        /// <summary>
        /// Gets the GUID of the group the command belongs to.
        /// </summary>
        public static readonly Guid CommandSet = Guids.guidGitHubCmdSet;

        /// <summary>
        /// Gets the numeric identifier of the command.
        /// </summary>
        public const int CommandId = PkgCmdIDList.openFromClipboardCommand;

        [ImportingConstructor]
        public OpenFromClipboardCommand(
            Lazy<IGitHubContextService> gitHubContextService,
            Lazy<ITeamExplorerContext> teamExplorerContext,
            Lazy<IVSServices> vsServices)
            : base(CommandSet, CommandId)
        {
            this.gitHubContextService = gitHubContextService;
            this.teamExplorerContext = teamExplorerContext;
            this.vsServices = vsServices;

            // See https://code.msdn.microsoft.com/windowsdesktop/AllowParams-2005-9442298f
            ParametersDescription = "u";    // accept a single url
        }

        public override async Task Execute(string url)
        {
            var context = gitHubContextService.Value.FindContextFromClipboard();
            if (context == null)
            {
                vsServices.Value.ShowMessageBoxInfo(NoGitHubUrlMessage);
                return;
            }

            var activeRepository = teamExplorerContext.Value.ActiveRepository;
            var repositoryDir = activeRepository?.LocalPath;
            if (repositoryDir == null)
            {
                vsServices.Value.ShowMessageBoxInfo(NoActiveRepositoryMessage);
                return;
            }

            var (commitish, path) = gitHubContextService.Value.ResolveBlob(repositoryDir, context);
            if (path == null)
            {
                vsServices.Value.ShowMessageBoxInfo(NoResolveMessage);
                return;
            }

            var hasChanges = gitHubContextService.Value.HasChangesInWorkingDirectory(repositoryDir, commitish, path);
            if (hasChanges)
            {
                // AnnotateFile expects a branch name so we use the current branch
                var branchName = activeRepository.CurrentBranch.Name;

                if (await gitHubContextService.Value.TryAnnotateFile(repositoryDir, branchName, context))
                {
                    return;
                }

                vsServices.Value.ShowMessageBoxInfo(ChangesInWorkingDirectoryMessage);
            }

            gitHubContextService.Value.TryOpenFile(repositoryDir, context);
        }
    }
}