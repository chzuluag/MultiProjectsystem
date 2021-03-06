// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.ProjectSystem.Build;
using Xunit;

namespace Microsoft.VisualStudio.ProjectSystem.Properties
{
    public class TargetFrameworkGlobalBuildPropertyProviderTests
    {
        [Fact]
        public async Task VerifyTargetFrameworkOverrideForCrossTargetingBuild()
        {
            var dimensions = Empty.PropertiesMap
                .Add("Configuration", "Debug")
                .Add("Platform", "AnyCPU")
                .Add("TargetFramework", "netcoreapp1.0");
            var projectConfiguration = new StandardProjectConfiguration("Debug|AnyCPU|netcoreapp1.0", dimensions);
            var configuredProject = ConfiguredProjectFactory.Create(projectConfiguration: projectConfiguration);
            var projectService = IProjectServiceFactory.Create();
            var provider = new TargetFrameworkGlobalBuildPropertyProvider(projectService, configuredProject);

            var properties = await provider.GetGlobalPropertiesAsync(CancellationToken.None);
            Assert.Single(properties);
            Assert.Equal("TargetFramework", properties.Keys.First());
            Assert.Equal(string.Empty, properties.Values.First());
        }

        [Fact]
        public async Task VerifyNoTargetFrameworkOverrideForRegularBuild()
        {
            var dimensions = Empty.PropertiesMap
                .Add("Configuration", "Debug")
                .Add("Platform", "AnyCPU");
            var projectConfiguration = new StandardProjectConfiguration("Debug|AnyCPU", dimensions);
            var configuredProject = ConfiguredProjectFactory.Create(projectConfiguration: projectConfiguration);
            var projectService = IProjectServiceFactory.Create();
            var provider = new TargetFrameworkGlobalBuildPropertyProvider(projectService, configuredProject);

            var properties = await provider.GetGlobalPropertiesAsync(CancellationToken.None);
            Assert.Equal(0, properties.Count);
        }
    }
}
