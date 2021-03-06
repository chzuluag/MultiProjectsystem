// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Microsoft.Build.Construction;

namespace Microsoft.Build.Evaluation
{
    internal static class ProjectFactory
    {
        public static Project Create(ProjectRootElement rootElement)
        {
            return new Project(rootElement);
        }
    }
}
