' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

Namespace Microsoft.VisualStudio.Editors.MyApplication

    Public Class MyApplicationPropertiesBase
        ''' <summary>
        ''' Returns the set of files that need to be checked out to change the given property
        ''' Must be overriden in sub-class
        ''' </summary>
        Public Overridable Function FilesToCheckOut(CreateIfNotExist As Boolean) As String()
            Return Array.Empty(Of String)
        End Function


    End Class ' Class MyApplicationPropertiesBase

End Namespace
