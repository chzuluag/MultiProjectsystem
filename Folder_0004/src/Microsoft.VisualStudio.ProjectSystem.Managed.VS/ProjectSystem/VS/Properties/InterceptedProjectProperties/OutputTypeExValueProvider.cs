// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.ProjectSystem.Properties;

namespace Microsoft.VisualStudio.ProjectSystem.VS.Properties
{
    /// <summary>
    /// OutputTypeEx acts as a converter for the MSBuild OutputType value expressed as <see cref="VSLangProj110.prjOutputTypeEx"/>.
    /// </summary>
    [ExportInterceptingPropertyValueProvider("OutputTypeEx", ExportInterceptingPropertyValueProviderFile.ProjectFile)]
    internal sealed class OutputTypeExValueProvider : OutputTypeValueProviderBase
    {
        private static readonly ImmutableDictionary<string, string> s_getOutputTypeExMap = new Dictionary<string, string>()
        {
            {"WinExe",          "0" },
            {"Exe",             "1" },
            {"Library",         "2" },
            {"WinMDObj",        "3" },
            {"AppContainerExe", "4" },
        }.ToImmutableDictionary(StringComparers.PropertyLiteralValues);

        private static readonly ImmutableDictionary<string, string> s_setOutputTypeExMap = new Dictionary<string, string>()
        {
            {"0", "WinExe" },
            {"1", "Exe" },
            {"2", "Library" },
            {"3", "WinMDObj"},
            {"4", "AppContainerExe" },
        }.ToImmutableDictionary(StringComparers.PropertyLiteralValues);

        [ImportingConstructor]
        public OutputTypeExValueProvider(ProjectProperties properties)
            : base(properties)
        {
        }

        protected override ImmutableDictionary<string, string> GetMap => s_getOutputTypeExMap;
        protected override ImmutableDictionary<string, string> SetMap => s_setOutputTypeExMap;
        protected override string DefaultGetValue => "0";
    }
}
