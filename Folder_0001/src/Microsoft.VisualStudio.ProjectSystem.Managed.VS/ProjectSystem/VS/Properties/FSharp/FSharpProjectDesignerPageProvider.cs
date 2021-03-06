// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Buffers.PooledObjects;

namespace Microsoft.VisualStudio.ProjectSystem.VS.Properties.FSharp
{
    /// <summary>
    ///     Provides project designer property pages.
    /// </summary>
    [Export(typeof(IVsProjectDesignerPageProvider))]
    [AppliesTo(ProjectCapability.FSharpAppDesigner)]
    internal class FSharpProjectDesignerPageProvider : IVsProjectDesignerPageProvider
    {
        private readonly IProjectCapabilitiesService _capabilities;

        [ImportingConstructor]
        internal FSharpProjectDesignerPageProvider(IProjectCapabilitiesService capabilities)
        {
            _capabilities = capabilities;
        }

        public Task<IReadOnlyCollection<IPageMetadata>> GetPagesAsync()
        {
            var builder = PooledArray<IPageMetadata>.GetInstance(capacity: 6);

            builder.Add(FSharpProjectDesignerPage.Application);
            builder.Add(FSharpProjectDesignerPage.Build);
            builder.Add(FSharpProjectDesignerPage.BuildEvents);

            if (_capabilities.Contains(ProjectCapability.LaunchProfiles))
            {
                builder.Add(FSharpProjectDesignerPage.Debug);
            }

            if (_capabilities.Contains(ProjectCapability.Pack))
            {
                builder.Add(FSharpProjectDesignerPage.Package);
            }

            builder.Add(FSharpProjectDesignerPage.ReferencePaths);

            return Task.FromResult<IReadOnlyCollection<IPageMetadata>>(builder.ToImmutableAndFree());
        }
    }
}
