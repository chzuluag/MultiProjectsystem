﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.Test.Apex.Hosts;
using Microsoft.Test.Apex.Services.Logging.TestLoggerSinks.OmniLog;
using Microsoft.Test.Apex.VisualStudio;

namespace Microsoft.VisualStudio
{
    internal class ProjectSystemHostConfiguration : VisualStudioHostConfiguration
    {
        // This combined with TestBase.IncludeReferencedAssembliesInHostComposition set to false, deliberately limit
        // the number of assemblies added to the composition to reduce MEF composition errors in the build log.
        internal static readonly string[] CompositionAssemblyPaths = new[] {
                    typeof(VisualStudioHostConfiguration).Assembly.Location,        // Microsoft.Test.Apex.VisualStudio
                    typeof(HostConfiguration).Assembly.Location,                    // Microsoft.Test.Apex.Framework
                    typeof(ProjectSystemHostConfiguration).Assembly.Location,       // This assembly
                    typeof(OmniLogSink).Assembly.Location,                          // Omni
                    };

        public ProjectSystemHostConfiguration()
        {
            CommandLineArguments = $"/rootSuffix {TestEnvironment.VisualStudioHive}";
            RestoreUserSettings = false;
            InheritProcessEnvironment = true;
            AutomaticallyDismissMessageBoxes = true;
            DelayInitialVsLicenseValidation = true;
            ForceFirstLaunch = false;
            BootstrapInjection = BootstrapInjectionMethod.DteFromROT;
        }

        public override IEnumerable<string> CompositionAssemblies => CompositionAssemblyPaths;
    }
}
