' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

Imports System.ComponentModel

Namespace Microsoft.VisualStudio.Editors.PropertyPages

    ''' <summary>
    '''  a sub class of DisplayNameAttribute to help localizing the property name...
    ''' </summary>
    <AttributeUsage(AttributeTargets.All)>
    Friend Class VBDisplayNameAttribute
        Inherits DisplayNameAttribute

        Private _replaced As Boolean

        Public Sub New(description As String)
            MyBase.New(description)
        End Sub

        Public Overrides ReadOnly Property DisplayName As String
            Get
                If Not _replaced Then
                    _replaced = True
                    DisplayNameValue = My.Resources.Microsoft_VisualStudio_Editors_Designer.ResourceManager.GetString(DisplayNameValue)
                End If
                Return DisplayNameValue
            End Get
        End Property
    End Class

End Namespace

