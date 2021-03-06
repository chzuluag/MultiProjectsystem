// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

namespace Microsoft.VisualStudio.ProjectSystem
{
    public enum ProjectTreeFormatError
    {
        IdExpected_EncounteredOnlyWhiteSpace,
        IdExpected_EncounteredDelimiter,
        IdExpected_EncounteredEndOfString,
        DelimiterExpected,
        DelimiterExpected_EncounteredEndOfString,
        EndOfStringExpected,
        UnrecognizedPropertyName,
        UnrecognizedPropertyValue,
        IndentTooManyLevels,
        MultipleRoots,
        IntegerExpected,
        GuidExpected,
    }
}
