'------------------------------------------------------------------------------
' <auto-generated>
'     This code was generated by a tool.
'     Runtime Version:4.0.30319.42000
'
'     Changes to this file may cause incorrect behavior and will be lost if
'     the code is regenerated.
' </auto-generated>
'------------------------------------------------------------------------------

Option Strict On
Option Explicit On

Imports System

Namespace My.Resources
    
    'This class was auto-generated by the StronglyTypedResourceBuilder
    'class via a tool like ResGen or Visual Studio.
    'To add or remove a member, edit your .ResX file then rerun ResGen
    'with the /str option, or rebuild your VS project.
    '''<summary>
    '''  A strongly-typed resource class, for looking up localized strings, etc.
    '''</summary>
    <Global.System.CodeDom.Compiler.GeneratedCodeAttribute("System.Resources.Tools.StronglyTypedResourceBuilder", "16.0.0.0"),  _
     Global.System.Diagnostics.DebuggerNonUserCodeAttribute(),  _
     Global.System.Runtime.CompilerServices.CompilerGeneratedAttribute()>  _
    Public Class GeneralOptionPageResources
        
        Private Shared resourceMan As Global.System.Resources.ResourceManager
        
        Private Shared resourceCulture As Global.System.Globalization.CultureInfo
        
        <Global.System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")>  _
        Friend Sub New()
            MyBase.New
        End Sub
        
        '''<summary>
        '''  Returns the cached ResourceManager instance used by this class.
        '''</summary>
        <Global.System.ComponentModel.EditorBrowsableAttribute(Global.System.ComponentModel.EditorBrowsableState.Advanced)>  _
        Public Shared ReadOnly Property ResourceManager() As Global.System.Resources.ResourceManager
            Get
                If Object.ReferenceEquals(resourceMan, Nothing) Then
                    Dim temp As Global.System.Resources.ResourceManager = New Global.System.Resources.ResourceManager("GeneralOptionPageResources", GetType(GeneralOptionPageResources).Assembly)
                    resourceMan = temp
                End If
                Return resourceMan
            End Get
        End Property
        
        '''<summary>
        '''  Overrides the current thread's CurrentUICulture property for all
        '''  resource lookups using this strongly typed resource class.
        '''</summary>
        <Global.System.ComponentModel.EditorBrowsableAttribute(Global.System.ComponentModel.EditorBrowsableState.Advanced)>  _
        Public Shared Property Culture() As Global.System.Globalization.CultureInfo
            Get
                Return resourceCulture
            End Get
            Set
                resourceCulture = value
            End Set
        End Property
        
        '''<summary>
        '''  Looks up a localized string similar to Don&apos;t call MSBuild if a project appears to be up to date..
        '''</summary>
        Public Shared ReadOnly Property General_FastUpToDateCheck() As String
            Get
                Return ResourceManager.GetString("General_FastUpToDateCheck", resourceCulture)
            End Get
        End Property
        
        '''<summary>
        '''  Looks up a localized string similar to Logging Level:.
        '''</summary>
        Public Shared ReadOnly Property General_FastUpToDateCheck_LogLevel() As String
            Get
                Return ResourceManager.GetString("General_FastUpToDateCheck_LogLevel", resourceCulture)
            End Get
        End Property
        
        '''<summary>
        '''  Looks up a localized string similar to Info.
        '''</summary>
        Public Shared ReadOnly Property General_FastUpToDateCheck_LogLevel_Info() As String
            Get
                Return ResourceManager.GetString("General_FastUpToDateCheck_LogLevel_Info", resourceCulture)
            End Get
        End Property
        
        '''<summary>
        '''  Looks up a localized string similar to Minimal.
        '''</summary>
        Public Shared ReadOnly Property General_FastUpToDateCheck_LogLevel_Minimal() As String
            Get
                Return ResourceManager.GetString("General_FastUpToDateCheck_LogLevel_Minimal", resourceCulture)
            End Get
        End Property
        
        '''<summary>
        '''  Looks up a localized string similar to None.
        '''</summary>
        Public Shared ReadOnly Property General_FastUpToDateCheck_LogLevel_None() As String
            Get
                Return ResourceManager.GetString("General_FastUpToDateCheck_LogLevel_None", resourceCulture)
            End Get
        End Property
        
        '''<summary>
        '''  Looks up a localized string similar to Verbose.
        '''</summary>
        Public Shared ReadOnly Property General_FastUpToDateCheck_LogLevel_Verbose() As String
            Get
                Return ResourceManager.GetString("General_FastUpToDateCheck_LogLevel_Verbose", resourceCulture)
            End Get
        End Property
        
        '''<summary>
        '''  Looks up a localized string similar to Up to Date Checks.
        '''</summary>
        Public Shared ReadOnly Property General_FastUpToDateCheck_Title() As String
            Get
                Return ResourceManager.GetString("General_FastUpToDateCheck_Title", resourceCulture)
            End Get
        End Property
    End Class
End Namespace
