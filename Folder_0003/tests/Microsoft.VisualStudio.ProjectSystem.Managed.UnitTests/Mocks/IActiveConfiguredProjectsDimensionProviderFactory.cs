// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Moq;

namespace Microsoft.VisualStudio.ProjectSystem.Configuration
{
    internal static class IActiveConfiguredProjectsDimensionProviderFactory
    {
        public static IActiveConfiguredProjectsDimensionProvider ImplementDimensionName(string value)
        {
            var mock = new Mock<IActiveConfiguredProjectsDimensionProvider>();
            mock.SetupGet(t => t.DimensionName)
                .Returns(value);

            return mock.Object;
        }
    }
}
