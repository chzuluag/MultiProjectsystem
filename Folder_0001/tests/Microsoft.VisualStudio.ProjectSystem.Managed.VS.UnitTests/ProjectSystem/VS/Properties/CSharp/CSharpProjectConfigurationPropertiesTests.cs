// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Microsoft.VisualStudio.ProjectSystem.VS.Properties.CSharp
{
    public class CSharpProjectConfigurationPropertiesTests
    {
        [Fact]
        public void Constructor_NullAsProjectProperties_ThrowsArgumentNull()
        {
            Assert.Throws<ArgumentNullException>("projectProperties", () =>
            {
                new CSharpProjectConfigurationProperties(null!, null!);
            });
        }

        [Fact]
        public void Constructor_NullAsThreadingService_ThrowsArgumentNull()
        {
            Assert.Throws<ArgumentNullException>("threadingService", () =>
            {
                new CSharpProjectConfigurationProperties(ProjectPropertiesFactory.CreateEmpty(), null!);
            });
        }

        [Fact]
        public void CSharpProjectConfigurationProperties_CodeAnalysisRuleSet()
        {
            var setValues = new List<object>();
            var project = UnconfiguredProjectFactory.Create();
            var data = new PropertyPageData(ConfiguredBrowseObject.SchemaName, ConfiguredBrowseObject.CodeAnalysisRuleSetProperty, "Blah", setValues);

            var projectProperties = ProjectPropertiesFactory.Create(project, data);

            var vsLangProjectProperties = CreateInstance(projectProperties, IProjectThreadingServiceFactory.Create());
            Assert.Equal("Blah", vsLangProjectProperties.CodeAnalysisRuleSet);

            var testValue = "Testing";
            vsLangProjectProperties.CodeAnalysisRuleSet = testValue;
            Assert.Equal(setValues.Single(), testValue);
        }

        [Fact]
        public void CSharpProjectConfigurationProperties_LangVersion()
        {
            var setValues = new List<object>();
            var project = UnconfiguredProjectFactory.Create();
            var data = new PropertyPageData(ConfiguredBrowseObject.SchemaName, ConfiguredBrowseObject.LangVersionProperty, "6", setValues);

            var projectProperties = ProjectPropertiesFactory.Create(project, data);

            var vsLangProjectProperties = CreateInstance(projectProperties, IProjectThreadingServiceFactory.Create());
            Assert.Equal("6", vsLangProjectProperties.LanguageVersion);

            var testValue = "7.1";
            vsLangProjectProperties.LanguageVersion = testValue;
            Assert.Equal(setValues.Single(), testValue);
        }

        [Fact]
        public void CSharpProjectConfigurationProperties_OutputPath()
        {
            var setValues = new List<object>();
            var project = UnconfiguredProjectFactory.Create();
            var data = new PropertyPageData(ConfiguredBrowseObject.SchemaName, ConfiguredBrowseObject.OutputPathProperty, "OldPath", setValues);

            var projectProperties = ProjectPropertiesFactory.Create(project, data);

            var vsLangProjectProperties = CreateInstance(projectProperties, IProjectThreadingServiceFactory.Create());
            Assert.Equal("OldPath", vsLangProjectProperties.OutputPath);

            var testValue = "newPath";
            vsLangProjectProperties.OutputPath = testValue;
            Assert.Equal(setValues.Single(), testValue);
        }

        private static CSharpProjectConfigurationProperties CreateInstance(ProjectProperties projectProperties, IProjectThreadingService projectThreadingService)
        {
            return new CSharpProjectConfigurationProperties(projectProperties, projectThreadingService);
        }
    }
}
