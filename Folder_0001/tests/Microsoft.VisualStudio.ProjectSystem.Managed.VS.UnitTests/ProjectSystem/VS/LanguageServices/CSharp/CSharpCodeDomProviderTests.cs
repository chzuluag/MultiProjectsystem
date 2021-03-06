// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Xunit;

namespace Microsoft.VisualStudio.ProjectSystem.VS.LanguageServices.CSharp
{
    public class CSharpCodeDomProviderTests
    {
        [Fact]
        public void Constructor_DoesNotThrow()
        {
            new CSharpCodeDomProvider();
        }
    }
}
