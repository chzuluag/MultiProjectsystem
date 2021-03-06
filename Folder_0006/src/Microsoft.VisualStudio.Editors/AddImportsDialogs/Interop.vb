' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

Imports System.Runtime.InteropServices

Namespace Microsoft.VisualStudio.Editors.AddImports

    <Guid("544D52A6-04C6-4771-863D-EFB1542C8025")>
    <InterfaceType(ComInterfaceType.InterfaceIsIUnknown)>
    <ComImport>
    Friend Interface IVBAddImportsDialogHelpCallback
        Sub InvokeHelp()
    End Interface

    Friend Enum AddImportsResult
        AddImports_Cancel = 1
        AddImports_ImportsAnyways = 2
        AddImports_QualifyCurrentLine = 3
    End Enum

    Friend Enum AddImportsDialogType
        AddImportsCollisionDialog = 1
        AddImportsExtensionCollisionDialog = 2
    End Enum

    <Guid("71CC3B66-3E89-45eb-BDDA-D6A5599F4C20")>
    <InterfaceType(ComInterfaceType.InterfaceIsIUnknown)>
    <ComImport>
    Friend Interface IVBAddImportsDialogService
        Function ShowDialog _
        (
            [namespace] As String,
            identifier As String,
            minimallyQualifiedName As String,
            dialogType As AddImportsDialogType,
            helpCallBack As IVBAddImportsDialogHelpCallback
        ) As AddImportsResult
    End Interface
End Namespace
