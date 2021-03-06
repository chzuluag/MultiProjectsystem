// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel.Composition;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using Microsoft.VisualStudio.ProjectSystem.Tree.ProjectImports;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Threading;
using IOleServiceProvider = Microsoft.VisualStudio.OLE.Interop.IServiceProvider;
using static Microsoft.VisualStudio.VSConstants;

namespace Microsoft.VisualStudio.ProjectSystem.VS.Tree.ProjectImports
{
    /// <summary>
    /// Handles opening of files displayed in the project imports tree.
    /// </summary>
    internal abstract class ProjectImportsCommandGroupHandlerBase : IAsyncCommandGroupHandler
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ConfiguredProject _configuredProject;
        private readonly IVsUIService<IVsUIShellOpenDocument> _uiShellOpenDocument;
        private readonly IVsUIService<IVsExternalFilesManager> _externalFilesManager;
        private readonly IVsUIService<IOleServiceProvider> _oleServiceProvider;

        protected ProjectImportsCommandGroupHandlerBase(
            IServiceProvider serviceProvider,
            ConfiguredProject configuredProject,
            IVsUIService<IVsUIShellOpenDocument> uiShellOpenDocument,
            IVsUIService<IVsExternalFilesManager> externalFilesManager,
            IVsUIService<IOleServiceProvider> oleServiceProvider)
        {
            Requires.NotNull(serviceProvider, nameof(serviceProvider));
            Requires.NotNull(configuredProject, nameof(configuredProject));
            Requires.NotNull(uiShellOpenDocument, nameof(uiShellOpenDocument));
            Requires.NotNull(externalFilesManager, nameof(externalFilesManager));
            Requires.NotNull(oleServiceProvider, nameof(oleServiceProvider));

            _serviceProvider = serviceProvider;
            _configuredProject = configuredProject;
            _uiShellOpenDocument = uiShellOpenDocument;
            _externalFilesManager = externalFilesManager;
            _oleServiceProvider = oleServiceProvider;
        }

        protected abstract bool IsOpenCommand(long commandId);

        protected abstract bool IsOpenWithCommand(long commandId);

        public Task<CommandStatusResult> GetCommandStatusAsync(IImmutableSet<IProjectTree> items, long commandId, bool focused, string? commandText, CommandStatus status)
        {
            if (items.Count != 0 && IsOpenCommand(commandId) && items.All(CanOpenFile))
            {
                status |= CommandStatus.Enabled | CommandStatus.Supported;
                return new CommandStatusResult(true, commandText, status).AsTask();
            }

            return CommandStatusResult.Unhandled.AsTask();
        }

        public Task<bool> TryHandleCommandAsync(IImmutableSet<IProjectTree> items, long commandId, bool focused, long commandExecuteOptions, IntPtr variantArgIn, IntPtr variantArgOut)
        {
            if (items.Count != 0 && IsOpenCommand(commandId) && items.All(CanOpenFile))
            {
                OpenItems();

                return TaskResult.True;
            }

            return TaskResult.False;

            void OpenItems()
            {
                IVsUIShellOpenDocument? uiShellOpenDocument = _uiShellOpenDocument.Value;
                Assumes.Present(uiShellOpenDocument);

                IOleServiceProvider? oleServiceProvider = _oleServiceProvider.Value;
                Assumes.Present(oleServiceProvider);

                IVsExternalFilesManager? externalFilesManager = _externalFilesManager.Value;
                Assumes.Present(externalFilesManager);

                var hierarchy = (IVsUIHierarchy)_configuredProject.UnconfiguredProject.Services.HostObject;
                var rdt = new RunningDocumentTable(_serviceProvider);

                // Open all items.
                RunAllAndAggregateExceptions(items, OpenItem);

                void OpenItem(IProjectTree item)
                {
                    IVsWindowFrame? windowFrame = null;
                    try
                    {
                        // Open the document.
                        Guid logicalView = IsOpenWithCommand(commandId) ? LOGVIEWID_UserChooseView : LOGVIEWID.Code_guid;
                        IntPtr docData = IntPtr.Zero;

                        ErrorHandler.ThrowOnFailure(
                            uiShellOpenDocument!.OpenStandardEditor(
                                (uint)__VSOSEFLAGS.OSE_ChooseBestStdEditor,
                                item.FilePath,
                                ref logicalView,
                                item.Caption,
                                hierarchy,
                                item.GetHierarchyId(),
                                docData,
                                oleServiceProvider,
                                out windowFrame));

                        RunningDocumentInfo rdtInfo = rdt.GetDocumentInfo(item.FilePath);

                        // Set it as read only if necessary.
                        bool isReadOnly = item.Flags.Contains(ImportTreeProvider.ProjectImportImplicit);

                        if (isReadOnly && rdtInfo.DocData is IVsTextBuffer textBuffer)
                        {
                            textBuffer.GetStateFlags(out uint flags);
                            textBuffer.SetStateFlags(flags | (uint)BUFFERSTATEFLAGS.BSF_USER_READONLY);
                        }

                        // Detach the document from this project.
                        // Ignore failure. It may be that we've already transferred the item to Miscellaneous Files.
                        externalFilesManager!.TransferDocument(item.FilePath, item.FilePath, windowFrame);

                        // Show the document window
                        if (windowFrame != null)
                        {
                            ErrorHandler.ThrowOnFailure(windowFrame.Show());
                        }
                    }
                    catch
                    {
                        windowFrame?.CloseFrame(0);
                        throw;
                    }
                }
            }
        }

        private static bool CanOpenFile(IProjectTree node) => node.Flags.Contains(ImportTreeProvider.ProjectImport);

        /// <summary>
        /// Calls <paramref name="action"/> for each of <paramref name="items"/>. If any action
        /// throws, its exception is caught and processing continues. When all items have been
        /// handled, any exceptions are thrown either as a single exception or an
        /// <see cref="AggregateException"/>.
        /// </summary>
        private static void RunAllAndAggregateExceptions<T>(IEnumerable<T> items, Action<T> action)
        {
            List<Exception>? exceptions = null;

            foreach (T item in items)
            {
                try
                {
                    action(item);
                }
                catch (Exception ex)
                {
                    exceptions ??= new List<Exception>();
                    exceptions.Add(ex);
                }
            }

            if (exceptions != null)
            {
                if (exceptions.Count == 1)
                {
                    ExceptionDispatchInfo.Capture(exceptions.First()).Throw();
                }
                else
                {
                    throw new AggregateException(exceptions);
                }
            }
        }

        [ExportCommandGroup(CMDSETID.UIHierarchyWindowCommandSet_string)]
        [AppliesTo(ProjectCapability.ProjectImportsTree)]
        [Order(ProjectSystem.Order.BeforeDefault)]
        private sealed class UIHierarchyWindowCommandSetGroupHandler : ProjectImportsCommandGroupHandlerBase
        {
            [ImportingConstructor]
            public UIHierarchyWindowCommandSetGroupHandler(
                [Import(typeof(SVsServiceProvider))] IServiceProvider serviceProvider,
                ConfiguredProject configuredProject,
                IVsUIService<SVsUIShellOpenDocument, IVsUIShellOpenDocument> uiShellOpenDocument,
                IVsUIService<SVsExternalFilesManager, IVsExternalFilesManager> externalFilesManager,
                IVsUIService<IOleServiceProvider> oleServiceProvider)
            : base(serviceProvider, configuredProject, uiShellOpenDocument, externalFilesManager, oleServiceProvider)
            {
            }

            protected override bool IsOpenCommand(long commandId)
            {
                return (VsUIHierarchyWindowCmdIds)commandId switch
                {
                    VsUIHierarchyWindowCmdIds.UIHWCMDID_DoubleClick => true,
                    VsUIHierarchyWindowCmdIds.UIHWCMDID_EnterKey => true,
                    _ => false
                };
            }

            protected override bool IsOpenWithCommand(long commandId) => false;
        }

        [ExportCommandGroup(CMDSETID.StandardCommandSet97_string)]
        [AppliesTo(ProjectCapability.ProjectImportsTree)]
        [Order(ProjectSystem.Order.BeforeDefault)]
        private sealed class StandardCommandSet97GroupHandler : ProjectImportsCommandGroupHandlerBase
        {
            [ImportingConstructor]
            public StandardCommandSet97GroupHandler(
                [Import(typeof(SVsServiceProvider))] IServiceProvider serviceProvider,
                ConfiguredProject configuredProject,
                IVsUIService<SVsUIShellOpenDocument, IVsUIShellOpenDocument> uiShellOpenDocument,
                IVsUIService<SVsExternalFilesManager, IVsExternalFilesManager> externalFilesManager,
                IVsUIService<IOleServiceProvider> oleServiceProvider)
                : base(serviceProvider, configuredProject, uiShellOpenDocument, externalFilesManager, oleServiceProvider)
            {
            }

            protected override bool IsOpenCommand(long commandId)
            {
                return (VSStd97CmdID)commandId switch
                {
                    VSStd97CmdID.Open => true,
                    VSStd97CmdID.OpenWith => true,
                    _ => false
                };
            }

            protected override bool IsOpenWithCommand(long commandId) => (VSStd97CmdID)commandId == VSStd97CmdID.OpenWith;
        }
    }
}
