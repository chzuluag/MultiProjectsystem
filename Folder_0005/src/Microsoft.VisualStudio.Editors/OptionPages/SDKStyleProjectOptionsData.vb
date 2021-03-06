' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

Imports System.ComponentModel
Imports System.Runtime.CompilerServices
Imports Microsoft.VisualStudio.Shell

Namespace Microsoft.VisualStudio.Editors.OptionPages
    ''' <summary>
    ''' Holds the data backing the Tools | Options | Projects and Solutions | SDK-Style Projects page.
    ''' </summary>
    Friend Class SDKStyleProjectOptionsData
        Implements INotifyPropertyChanged

        Public Shared ReadOnly Property MainInstance As SDKStyleProjectOptionsData = New SDKStyleProjectOptionsData

        Public Event PropertyChanged As PropertyChangedEventHandler Implements INotifyPropertyChanged.PropertyChanged

        Private _fastUpToDateCheckEnabled As Boolean = True
        Private _fastUpToDateCheckLogLevel As LogLevel = LogLevel.None

        Public Function Clone() As SDKStyleProjectOptionsData
            Dim clonedData = New SDKStyleProjectOptionsData
            clonedData.CopyFrom(Me)
            Return clonedData
        End Function

        Public Sub CopyFrom(source As SDKStyleProjectOptionsData)
            FastUpToDateCheckEnabled = source.FastUpToDateCheckEnabled
            FastUpToDateCheckLogLevel = source.FastUpToDateCheckLogLevel
        End Sub

        <SharedSettings("ManagedProjectSystem\FastUpToDateCheckEnabled", False)>
        Public Property FastUpToDateCheckEnabled As Boolean
            Get
                Return _fastUpToDateCheckEnabled
            End Get
            Set
                If value = _fastUpToDateCheckEnabled Then
                    Return
                End If

                _fastUpToDateCheckEnabled = value
                SendPropertyChangedNotification()
            End Set
        End Property

        <SharedSettings("ManagedProjectSystem\FastUpToDateLogLevel", False)>
        Public Property FastUpToDateCheckLogLevel As LogLevel
            Get
                Return _fastUpToDateCheckLogLevel
            End Get
            Set
                If value = _fastUpToDateCheckLogLevel Then
                    Return
                End If

                _fastUpToDateCheckLogLevel = value
                SendPropertyChangedNotification()
            End Set
        End Property

        Private Sub SendPropertyChangedNotification(<CallerMemberName> Optional callingMember As String = Nothing)
            RaiseEvent PropertyChanged(Me, New PropertyChangedEventArgs(callingMember))
        End Sub
    End Class
End Namespace

