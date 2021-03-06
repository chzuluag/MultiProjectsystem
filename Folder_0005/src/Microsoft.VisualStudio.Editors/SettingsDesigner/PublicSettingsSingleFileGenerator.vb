' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

Imports System.Reflection
Imports System.Runtime.InteropServices

Namespace Microsoft.VisualStudio.Editors.SettingsDesigner

    ''' <summary>
    ''' Generator for strongly typed settings wrapper class
    ''' </summary>
    <Guid("940f36b5-a42e-435e-8ef4-20b9d4801d22")>
    Public Class PublicSettingsSingleFileGenerator
        Inherits SettingsSingleFileGeneratorBase

        Public Const SingleFileGeneratorName As String = "PublicSettingsSingleFileGenerator"

        ''' <summary>
        ''' Returns the default visibility of this properties
        ''' </summary>
        ''' <value>MemberAttributes indicating what visibility to make the generated properties.</value>
        Friend Overrides ReadOnly Property SettingsClassVisibility As TypeAttributes
            Get
                Return TypeAttributes.Sealed Or TypeAttributes.Public
            End Get
        End Property

    End Class
End Namespace
