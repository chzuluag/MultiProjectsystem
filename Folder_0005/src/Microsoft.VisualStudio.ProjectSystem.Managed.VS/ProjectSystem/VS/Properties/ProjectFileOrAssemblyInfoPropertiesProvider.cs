// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.LanguageServices;
using Microsoft.VisualStudio.ProjectSystem.LanguageServices;
using Microsoft.VisualStudio.ProjectSystem.Properties;

namespace Microsoft.VisualStudio.ProjectSystem.VS.Properties
{
    /// <summary>
    /// A provider for properties that are stored either in the project file OR in the source code of the project.
    /// This is defined in the VS layer so that we can import <see cref="VisualStudioWorkspace"/>.
    /// </summary>
    [Export("ProjectFileOrAssemblyInfo", typeof(IProjectPropertiesProvider))]
    [Export(typeof(IProjectPropertiesProvider))]
    [ExportMetadata("Name", "ProjectFileOrAssemblyInfo")]
    [AppliesTo(ProjectCapability.DotNet)]
    internal class ProjectFileOrAssemblyInfoPropertiesProvider : AbstractProjectFileOrAssemblyInfoPropertiesProvider
    {
        [ImportingConstructor]
        public ProjectFileOrAssemblyInfoPropertiesProvider(
            [Import(ContractNames.ProjectPropertyProviders.ProjectFile)] IProjectPropertiesProvider delegatedProvider,
            [Import(ContractNames.ProjectPropertyProviders.ProjectFile)] IProjectInstancePropertiesProvider instanceProvider,
            [ImportMany(ContractNames.ProjectPropertyProviders.ProjectFile)]IEnumerable<Lazy<IInterceptingPropertyValueProvider, IInterceptingPropertyValueProviderMetadata>> interceptingValueProviders,
            UnconfiguredProject project,
            IActiveWorkspaceProjectContextHost projectContextHost,
            VisualStudioWorkspace workspace,
            IProjectThreadingService threadingService)
            : base(delegatedProvider, instanceProvider, interceptingValueProviders, project,
                  getActiveProjectId: () => GetProjectId(threadingService, projectContextHost),
                  workspace: workspace,
                  threadingService: threadingService)
        {
        }

        private static ProjectId GetProjectId(IProjectThreadingService threadingService, IActiveWorkspaceProjectContextHost projectContextHost)
        {
            return threadingService.ExecuteSynchronously(() =>
            {
                return projectContextHost.OpenContextForWriteAsync(accessor =>
                {
                    return Task.FromResult(accessor.Context.Id);
                });
            });
        }
    }
}
