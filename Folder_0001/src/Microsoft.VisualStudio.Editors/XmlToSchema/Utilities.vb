' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

Option Infer On
Imports System.ComponentModel
Imports System.Diagnostics.CodeAnalysis
Imports System.Drawing
Imports System.Security.Permissions
Imports System.Windows.Forms

Imports Microsoft.VisualStudio.Editors.DesignerFramework

Imports VsShell = Microsoft.VisualStudio.Shell.Interop

Namespace Microsoft.VisualStudio.Editors.XmlToSchema
    ''' <summary>
    ''' Utility method for the XmlToSchema wizard.
    ''' </summary>
    Friend Module Utilities
        Public Sub ShowWarning(message As String)
            DesignUtil.ShowWarning(VBPackage.Instance, message)
        End Sub

        Public Sub ShowWarning(ex As Exception)
            ShowWarning(String.Format(My.Resources.Microsoft_VisualStudio_Editors_Designer.XmlToSchema_Error, ex.Message))
        End Sub

        Public Function FilterException(ex As Exception) As Boolean
            Return Not TypeOf ex Is AccessViolationException AndAlso
                   Not TypeOf ex Is StackOverflowException AndAlso
                   Not TypeOf ex Is OutOfMemoryException
        End Function
    End Module

    ''' <summary>
    ''' A common base class for all XmlToSchema wizard forms.
    ''' </summary>
    Friend MustInherit Class XmlToSchemaForm
        Inherits Form

        Private _serviceProvider As IServiceProvider

        Protected Sub New()
            HelpButton = True
        End Sub

        Protected Sub New(serviceProvider As IServiceProvider)
            _serviceProvider = serviceProvider
            HelpButton = True
        End Sub

        Public Property ServiceProvider As IServiceProvider
            Get
                Return _serviceProvider
            End Get
            Set
                _serviceProvider = value
            End Set
        End Property

        Protected ReadOnly Property DialogFont As Font
            Get
                Dim hostLocale As VsShell.IUIHostLocale2 = CType(_serviceProvider.GetService(GetType(VsShell.SUIHostLocale)), VsShell.IUIHostLocale2)
                If hostLocale IsNot Nothing Then
                    Dim fonts(1) As VsShell.UIDLGLOGFONT
                    If VSErrorHandler.Succeeded(hostLocale.GetDialogFont(fonts)) Then
                        Return Font.FromLogFont(fonts(0))
                    End If
                End If
                Debug.Fail("Couldn't get a IUIHostLocale2 ... cheating instead :)")
                Return DefaultFont
            End Get
        End Property

        Protected Overrides Sub OnLoad(e As EventArgs)
            Debug.Assert(_serviceProvider IsNot Nothing)
            If _serviceProvider IsNot Nothing Then
                Font = DialogFont
            End If
            MyBase.OnLoad(e)
        End Sub

        Protected Overridable Function GetF1Keyword() As String
            Return "vb.XmlToSchemaWizard"
        End Function

        <SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes")>
        Private Sub ShowHelp()
            Try
                Dim f1Word = GetF1Keyword()
                If _serviceProvider IsNot Nothing AndAlso Not String.IsNullOrEmpty(f1Word) Then
                    Dim vshelp As VSHelp.Help = CType(_serviceProvider.GetService(GetType(VSHelp.Help)), VSHelp.Help)
                    vshelp.DisplayTopicFromF1Keyword(f1Word)
                Else
                    Debug.Fail("Can not find ServiceProvider")
                End If
            Catch ex As Exception
                Debug.Fail("Unexpected exception during Help invocation " + ex.Message)
            End Try
        End Sub

        Protected NotOverridable Overrides Sub OnHelpButtonClicked(e As CancelEventArgs)
            ShowHelp()
        End Sub

        Protected NotOverridable Overrides Sub OnHelpRequested(hevent As HelpEventArgs)
            ShowHelp()
            hevent.Handled = True
        End Sub

        <SecurityPermission(SecurityAction.LinkDemand, Flags:=SecurityPermissionFlag.UnmanagedCode)>
        <SecurityPermission(SecurityAction.InheritanceDemand, Flags:=SecurityPermissionFlag.UnmanagedCode)>
        Protected Overrides Sub WndProc(ByRef m As Message)
            Try
                Select Case m.Msg
                    Case Interop.Win32Constant.WM_SYSCOMMAND
                        If CInt(m.WParam) = Interop.Win32Constant.SC_CONTEXTHELP Then
                            ShowHelp()
                        Else
                            MyBase.WndProc(m)
                        End If
                    Case Else
                        MyBase.WndProc(m)
                End Select
            Catch ex As Exception
                If Not FilterException(ex) Then
                    Throw
                End If
            End Try
        End Sub
    End Class
End Namespace
