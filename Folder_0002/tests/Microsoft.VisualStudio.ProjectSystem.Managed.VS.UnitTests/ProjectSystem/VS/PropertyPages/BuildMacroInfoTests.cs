﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Moq;
using Xunit;

namespace Microsoft.VisualStudio.ProjectSystem.VS.PropertyPages
{
    public class BuildMacroInfoTests
    {
        [Theory]
        [InlineData("MyBuildMacro", "MyBuildMacroValue", VSConstants.S_OK)]
        [InlineData("NonExistantMacro", "", VSConstants.E_FAIL)]
        public void GetBuildMacroValue(string macroName, string expectedValue, int expectedRetVal)
        {
            var projectProperties = IProjectPropertiesFactory.CreateWithPropertyAndValue("MyBuildMacro", "MyBuildMacroValue");
            var propertiesProvider = IProjectPropertiesProviderFactory.Create(commonProps: projectProperties);
            var configuredProjectServices = Mock.Of<ConfiguredProjectServices>(o =>
                o.ProjectPropertiesProvider == propertiesProvider);
            var configuredProject = ConfiguredProjectFactory.Create(services: configuredProjectServices);

            var buildMacroInfo = CreateInstance(configuredProject);
            int retVal = buildMacroInfo.GetBuildMacroValue(macroName, out string? macroValue);
            Assert.Equal(expectedRetVal, retVal);
            Assert.Equal(expectedValue, macroValue);
        }

        [Fact]
        public void GetBuildMacroValue_WhenDisposed_ReturnsUnexpected()
        {
            var buildMacroInfo = CreateInstance();
            buildMacroInfo.Dispose();

            var result = buildMacroInfo.GetBuildMacroValue("Macro", out _);

            Assert.Equal(VSConstants.E_UNEXPECTED, result);
        }

        private static BuildMacroInfo CreateInstance(ConfiguredProject? configuredProject = null)
        {
            configuredProject ??= ConfiguredProjectFactory.Create();

            var threadingService = IProjectThreadingServiceFactory.Create();

            return new BuildMacroInfo(ActiveConfiguredProjectFactory.ImplementValue(() => configuredProject), threadingService);
        }
    }
}
