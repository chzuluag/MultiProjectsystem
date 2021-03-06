// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using Xunit;

namespace Microsoft.VisualStudio.ProjectSystem.Imaging.VisualBasic
{
    public class VisualBasicProjectImageProviderTests
    {
        [Fact]
        public void Constructor_DoesNotThrow()
        {
            new VisualBasicProjectImageProvider();
        }

        [Fact]
        public void GetProjectImage_NullAsKey_ThrowsArgumentNull()
        {
            var provider = CreateInstance();

            Assert.Throws<ArgumentNullException>("key", () =>
            {
                provider.GetProjectImage(null!);
            });
        }

        [Fact]
        public void GetProjectImage_EmptyAsKey_ThrowsArgument()
        {
            var provider = CreateInstance();

            Assert.Throws<ArgumentException>("key", () =>
            {
                provider.GetProjectImage(string.Empty);
            });
        }

        [Fact]
        public void GetProjectImage_UnrecognizedKeyAsKey_ReturnsNull()
        {
            var provider = CreateInstance();

            var result = provider.GetProjectImage("Unrecognized");

            Assert.Null(result);
        }

        [Theory]
        [InlineData(ProjectImageKey.ProjectRoot)]
        [InlineData(ProjectImageKey.SharedProjectRoot)]
        [InlineData(ProjectImageKey.SharedItemsImportFile)]
        public void GetProjectImage_RecognizedKeyAsKey_ReturnsNonNull(string key)
        {
            var provider = CreateInstance();

            var result = provider.GetProjectImage(key);

            Assert.NotNull(result);
        }

        private static VisualBasicProjectImageProvider CreateInstance()
        {
            return new VisualBasicProjectImageProvider();
        }
    }
}
