' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

Option Strict On
Option Explicit On
Imports System.ComponentModel.Design
Imports System.IO
Imports System.Runtime.InteropServices
Imports System.Threading
Imports System.Xml
Imports System.Xml.Schema

Imports Microsoft.VisualStudio.Shell.Interop
Imports Microsoft.VisualStudio.WCFReference.Interop
Imports Microsoft.VisualStudio.XmlEditor

Imports NativeMethods = Microsoft.VisualStudio.ErrorHandler

Namespace Microsoft.VisualStudio.Editors.XmlIntellisense

    '--------------------------------------------------------------------------
    ' XmlIntellisenseService:
    '   XmlIntellisense Service Class. Implements the IXmlIntellisenseService
    '   interface.
    '
    '   This class is a factory for XmlIntellisenseSchemas objects, so that
    '   each VB project can have its own instance of XmlIntellisenseSchemas.
    '
    '   Methods on this class should only be called on the VS foreground
    '   thread.
    '--------------------------------------------------------------------------
    <ClassInterface(ClassInterfaceType.None)>
    Friend NotInheritable Class XmlIntellisenseService
        Implements IXmlIntellisenseService

        Private ReadOnly _container As IServiceContainer
        Private ReadOnly _schemaService As XmlSchemaService

        '--------------------------------------------------------------------------
        ' New:
        '   Initialize the class.
        '--------------------------------------------------------------------------
        Friend Sub New(Container As IServiceContainer, SchemaService As XmlSchemaService)
            _container = Container
            _schemaService = SchemaService
        End Sub

        '--------------------------------------------------------------------------
        ' CreateSchemas:
        '   Implements IXmlIntellisenseService.CreateSchemas
        '
        '   Create an instance of XmlIntellisenseSchemas for the project with the
        '   specified GUID.
        '--------------------------------------------------------------------------
        Public Function CreateSchemas(ProjectGuid As Guid) As IXmlIntellisenseSchemas _
            Implements IXmlIntellisenseService.CreateSchemas

            Return New XmlIntellisenseSchemas(_container, _schemaService, ProjectGuid)
        End Function
    End Class

    ' This is the 'instance fields' for XmlIntellisenseSchemas.  It is in a separate class, so that GC-rooted callbacks can reference this without 
    ' also rooting the owner XmlIntellisenseSchemas instance object. As a result, we can witness the lifetime of the owner XmlIntellisenseSchemas 
    ' instance object via its finalizer, and leverage that lifetime information to prevent leaks.
    Friend Class XmlIntellisenseSchemasData
        Public Container As IServiceContainer
        Public SchemaService As XmlSchemaService
        Public ProjectGuid As Guid
        Public Hierarchy As IVsHierarchy
        Public SchemasCompiledEvent As ManualResetEvent
        Public Builder As XmlSchemaSetBuilder
        Public TargetNamespaces() As String
        Public SchemaSet As XmlSchemaSet
        Public IndexedMembers As IXmlIntellisenseMemberList
        Public FirstErrorSource As String
        Public ExcludeDirectories As Dictionary(Of Uri, Uri)
        Public CompilationLevel As Integer
        Public SchemasFound As Integer
        Public SchemasCompilationCallBackDoneEvent As ManualResetEvent
        Private _ownerHasBeenFinalized As Boolean

        Public Sub New()
            _ownerHasBeenFinalized = False
        End Sub

        Public Sub NotifyOwnerIsFinalized()
            _ownerHasBeenFinalized = True
        End Sub

        Public Function OwnerHasBeenFinalized() As Boolean
            Return _ownerHasBeenFinalized
        End Function
    End Class

    '--------------------------------------------------------------------------
    ' XmlIntellisenseSchemas:
    '   XmlIntellisense Service Class. Implements the IXmlIntellisenseSchemas
    '   interface.
    '
    '   This class allows compilation of Xml schema information derived from
    '   schemas in the project.  Each time schema information is needed,
    '   AsyncCompile() should be called.  When compilation is complete, the
    '   CompiledEvent will be signaled (note that this is an OS Event, not a
    '   CLR Event).
    '
    '   Methods on this class should only be called on the VS foreground
    '   thread.
    '--------------------------------------------------------------------------
    <ClassInterface(ClassInterfaceType.None)>
    Friend NotInheritable Class XmlIntellisenseSchemas
        Implements IXmlIntellisenseSchemas

        Private Const MaxPollInterval As Integer = 60 * 1000 ' 1 minutes
        Private Const MinPollInterval As Integer = 1000 ' 1 second

        ' Number of calls into AsyncCompile prior to terminating initial compilation loop
        Private Const CompilationLevel As Integer = 2

        Private ReadOnly _data As XmlIntellisenseSchemasData
        '--------------------------------------------------------------------------
        ' New:
        '   Initialize the class.
        '--------------------------------------------------------------------------
        Friend Sub New(Container As IServiceContainer, SchemaService As XmlSchemaService, ProjectGuid As Guid)
            _data = New XmlIntellisenseSchemasData With {
                .Container = Container,
                .SchemaService = SchemaService,
                .ProjectGuid = ProjectGuid,
                .CompilationLevel = CompilationLevel,
                .SchemasFound = 0
            }

            ' Get VS project
            Dim Solution As IVsSolution = DirectCast(Container.GetService(GetType(IVsSolution)), IVsSolution)
            Solution.GetProjectOfGuid(ProjectGuid, _data.Hierarchy)

            _data.SchemasCompiledEvent = New ManualResetEvent(True)
            _data.SchemasCompilationCallBackDoneEvent = New ManualResetEvent(True)
            _data.Builder = _data.SchemaService.CreateSchemaSetBuilder()
        End Sub

        Protected Overrides Sub Finalize()
            _data.NotifyOwnerIsFinalized()
        End Sub

        '--------------------------------------------------------------------------
        ' AsyncCompile:
        '   Implements IXmlIntellisenseSchemas.AsyncCompile
        '
        '   Recompiles all schemas in the specified project, ensuring that the most
        '   up-to-date files have been loaded.  Once compilation is complete, then
        '   the CompiledEvent will be signaled.
        '
        '   This method must be called on the VS foreground thread.  Also, in order
        '   for the background thread to make progress, the foreground thread must
        '   pump messages while waiting for any compilation results.
        '--------------------------------------------------------------------------
        Public Sub AsyncCompile() _
            Implements IXmlIntellisenseSchemas.AsyncCompile

            ' If event is signaled, then previous compilation is complete, so start another
            If _data.SchemasCompilationCallBackDoneEvent.WaitOne(0, True) Then

                ' Exclude schemas that were auto-generated by adding service references, as they can contain conflicts
                Dim ExcludeDirectories As Dictionary(Of Uri, Uri) = Nothing

                Dim vsWCFMetadataStorageProvider = TryCast(_data.Hierarchy, IVsWCFMetadataStorageProvider)
                If vsWCFMetadataStorageProvider IsNot Nothing Then
                    Dim Storages As IVsEnumWCFMetadataStorages = vsWCFMetadataStorageProvider.GetStorages()
                    Dim Storage(1) As IVsWCFMetadataStorage
                    Dim ReturnCount As UInteger

                    While NativeMethods.Succeeded(Storages.Next(1, Storage, ReturnCount)) AndAlso ReturnCount > 0
                        Dim ResultUri As Uri = Nothing
                        If Storage(0) IsNot Nothing AndAlso Uri.TryCreate(Path.GetDirectoryName(Storage(0).GetMapFilePath()), UriKind.Absolute, ResultUri) Then
                            If ExcludeDirectories Is Nothing Then
                                ExcludeDirectories = New Dictionary(Of Uri, Uri)()
                            End If
                            ExcludeDirectories(ResultUri) = ResultUri
                        End If
                    End While
                End If

                ' Publish the excluded schemas to a member variable so that the background thread can safely access it
                ' It is important that this dictionary's content are immutable, since they will be accessed by the background thread
                _data.ExcludeDirectories = ExcludeDirectories

                ' Reset event and start background thread
                _data.SchemasCompilationCallBackDoneEvent.Reset()

                ThreadPool.QueueUserWorkItem(Sub() CompileCallBack(_data))
            End If

            If _data.CompilationLevel > 0 Then
                _data.CompilationLevel -= 1
            End If
        End Sub

        '--------------------------------------------------------------------------
        ' CompiledEvent:
        '   Implements IXmlIntellisenseSchemas.CompiledEvent
        '
        '   Get a raw handle to the event which will be signaled when the
        '   background thread has completed compilation.
        '
        '   IMPORTANT: Do not attempt to use the event once this object has been
        '              released.
        '--------------------------------------------------------------------------
        Public ReadOnly Property CompiledEvent As IntPtr _
            Implements IXmlIntellisenseSchemas.CompiledEvent

            Get
                Return _data.SchemasCompiledEvent.SafeWaitHandle.DangerousGetHandle()
            End Get
        End Property

        '--------------------------------------------------------------------------
        ' TargetNamespaces:
        '   Implements IXmlIntellisenseSchemas.TargetNamespaces
        '
        '   Gets array of all result target namespaces, which will form the basis
        '   of a dropdown list for the user to pick from.
        '--------------------------------------------------------------------------
        Public ReadOnly Property TargetNamespaces As String() _
            Implements IXmlIntellisenseSchemas.TargetNamespaces

            Get
                Return _data.TargetNamespaces
            End Get
        End Property

        '--------------------------------------------------------------------------
        ' MemberList:
        '   Implements IXmlIntellisenseSchemas.MemberList
        '
        '   Returns a list of IXmlIntellisenseMember objects, one for each element
        '   and attribute declaration in the schema set.  This list can be filtered
        '   by calling the various filter methods on IXmlIntellisenseMember.
        '--------------------------------------------------------------------------
        Public ReadOnly Property MemberList As IXmlIntellisenseMemberList _
            Implements IXmlIntellisenseSchemas.MemberList

            Get
                Return _data.IndexedMembers
            End Get
        End Property

        '--------------------------------------------------------------------------
        ' FirstErrorSource:
        '   Implements IXmlIntellisenseSchemas.FirstErrorSource
        '
        '   If non-null, contains the source uri of the file which triggered the
        '   first error (not warning) while compiling the schema set.
        '--------------------------------------------------------------------------
        Public ReadOnly Property FirstErrorSource As String _
            Implements IXmlIntellisenseSchemas.FirstErrorSource

            Get
                Return _data.FirstErrorSource
            End Get
        End Property

        Private Shared Async Sub CompileCallBack(data As XmlIntellisenseSchemasData)
            Dim ProjectSchemas As IList(Of XmlSchemaReference)
            Dim pollInterval As Integer = MinPollInterval
            Dim iteration As Integer = 0

            If data.CompilationLevel > 0 OrElse iteration = 0 Then
                ' If owner was finalized, then end this polling loop, to prevent a leak
                While (Not data.OwnerHasBeenFinalized()) And (data.CompilationLevel > 0 OrElse iteration = 0)
                    ' Get all schemas in the current project
                    ProjectSchemas = data.SchemaService.GetKnownSchemas(data.ProjectGuid)

                    If ProjectSchemas.Count <> data.SchemasFound OrElse iteration = 0 Then
                        data.SchemasFound = ProjectSchemas.Count
                        Compile(ProjectSchemas, data)
                    Else
                        pollInterval *= 2

                        If pollInterval > MaxPollInterval Then
                            pollInterval = MaxPollInterval
                        End If
                    End If

                    Await Task.Delay(pollInterval)
                    iteration += 1
                End While
            Else
                Compile(data.SchemaService.GetKnownSchemas(data.ProjectGuid), data)
            End If
            data.SchemasCompilationCallBackDoneEvent.Set()
        End Sub

        '--------------------------------------------------------------------------
        ' Compile:
        '   Called from CompileCallback when schemas need to be compiled
        '--------------------------------------------------------------------------
        Private Shared Sub Compile(ProjectSchemas As IList(Of XmlSchemaReference), data As XmlIntellisenseSchemasData)

            ' Now signal that we are not done with compilation yet
            data.SchemasCompiledEvent.Reset()

            ' Include all schemas in the current project in the schema set except those in excluded directories
            data.Builder.Sources.Clear()
            For Each Source As XmlSchemaReference In ProjectSchemas
                If data.ExcludeDirectories IsNot Nothing AndAlso Source.Location.IsFile Then
                    Dim ResultUri As Uri = Nothing
                    If Uri.TryCreate(Path.GetDirectoryName(Source.Location.LocalPath), UriKind.Absolute, ResultUri) AndAlso
                       data.ExcludeDirectories.ContainsKey(ResultUri) Then

                        Continue For
                    End If
                End If

                data.Builder.Sources.Add(Source)
            Next

            ' Add all schemas from the schema cache as candidates (i.e. schemas which will be used to resolve dangling Tns references in the sources)
            ' Enable next line once newest Xml Editor Schema Service reaches Main
            '            data.m_Builder.Candidates = data.m_SchemaService.GetKnownSchemas(XmlSchemaService.GUID_SchemaCache)

            ' Compile the schema set
            data.Builder.Compile()

            ' Save the first non-warning XmlSchemaReferenceException, if there is one
            Dim FirstErrorSource As String = Nothing
            For Each EachError As Exception In data.Builder.Errors

                ' Ignore warnings
                If EachError.Data("SchemaServices.Warning") Is Nothing Then
                    Dim SourceException As XmlSchemaReferenceException = TryCast(EachError, XmlSchemaReferenceException)

                    If SourceException IsNot Nothing AndAlso SourceException.InnerException Is Nothing Then
                        ' An XmlSchemaReferenceException will exist if an error loading one of the schemas occurred
                        FirstErrorSource = SourceException.Reference.Location.OriginalString
                        Exit For
                    ElseIf ProjectSchemas.Count > 0 Then
                        ' If no XmlSchemaReferenceException exists, then there must have been a set compilation error,
                        ' so get one of the source schemas, as it should be able to report the exact nature of the error
                        ' after the user navigates to it.
                        FirstErrorSource = ProjectSchemas(0).Location.OriginalString
                    End If
                End If
            Next
            data.FirstErrorSource = FirstErrorSource

            ' If the schema set is unchanged, then no need to rebuild index over it
            If data.SchemaSet IsNot data.Builder.CompiledSet Then
                data.SchemaSet = data.Builder.CompiledSet

                ' Do not build index or collect target namespaces if any errors occurred
                If FirstErrorSource Is Nothing Then
                    ' Save the target namespaces of all the result schemas
                    Dim UniqueNamespaces As Dictionary(Of String, String) = New Dictionary(Of String, String)
                    Dim ResolvedSet As IList(Of XmlSchemaReference) = data.Builder.ResolvedSet

                    If ResolvedSet.Count = 0 Then
                        Dim TargetNamespaces As String() = Array.Empty(Of String)
                        data.TargetNamespaces = TargetNamespaces
                    Else
                        For Index As Integer = 0 To ResolvedSet.Count - 1
                            UniqueNamespaces(ResolvedSet(Index).TargetNamespace) = ResolvedSet(Index).TargetNamespace
                        Next

                        Dim TargetNamespaces(UniqueNamespaces.Count) As String
                        UniqueNamespaces.Values.CopyTo(TargetNamespaces, 0)

                        data.TargetNamespaces = TargetNamespaces
                    End If

                    ' Build the index
                    data.IndexedMembers = New IndexedMembers(data.SchemaSet, UniqueNamespaces).All
                Else
                    data.IndexedMembers = Nothing
                    data.TargetNamespaces = Nothing
                End If
            End If

            ' Now notify any listener that compilation is complete
            data.SchemasCompiledEvent.Set()

        End Sub

        '--------------------------------------------------------------------------
        ' ShowInXmlSchameExplorer:
        '   Implements IXmlIntellisenseSchemas.ShowInXmlSchemaExplorer
        '
        '   Shows element in XSD browser given namespace and local name.
        '--------------------------------------------------------------------------
        Public ReadOnly Property IsEmpty As <MarshalAs(UnmanagedType.Bool)> Boolean _
            Implements IXmlIntellisenseSchemas.IsEmpty
            Get
                Return _data.SchemaSet Is Nothing OrElse _data.SchemaSet.Count = 0
            End Get
        End Property

        '--------------------------------------------------------------------------
        ' ShowInXmlSchameExplorer:
        '   Implements IXmlIntellisenseSchemas.ShowInXmlSchemaExplorer
        '
        '   Shows element in XSD browser given namespace and local name.
        '--------------------------------------------------------------------------
        Public Sub ShowInXmlSchemaExplorer(
            <[In], MarshalAs(UnmanagedType.BStr)> NamespaceName As String,
            <[In], MarshalAs(UnmanagedType.BStr)> LocalName As String,
            <MarshalAs(UnmanagedType.Bool)> ByRef ElementFound As Boolean,
            <MarshalAs(UnmanagedType.Bool)> ByRef NamespaceFound As Boolean) _
            Implements IXmlIntellisenseSchemas.ShowInXmlSchemaExplorer

            ElementFound = False
            NamespaceFound = False

            If _data.SchemaSet Is Nothing OrElse _data.IndexedMembers Is Nothing Then Return

            ' Find the XmlSchemaElement based on NamespaceName and LocalName provided.
            Dim element As XmlSchemaElement = Nothing
            Dim ns As XNamespace = Nothing
            If NamespaceName Is Nothing Then
                NamespaceName = String.Empty
            End If
            If LocalName IsNot Nothing Then
                Dim elements As IXmlIntellisenseMemberList = _data.IndexedMembers.ElementsByName(NamespaceName, LocalName)
                If elements IsNot Nothing Then
                    Dim enumerator As IXmlIntellisenseMemberEnumerator = elements.GetEnumerator()
                    If enumerator IsNot Nothing Then
                        Dim member As XmlIntellisenseMember = TryCast(enumerator.GetNext(), XmlIntellisenseMember)
                        If member IsNot Nothing Then
                            element = member.Element
                        End If
                    End If
                End If
            End If
            ElementFound = element IsNot Nothing
            If NamespaceName IsNot Nothing Then
                Try
                    ' Get 'ns' even if 'element' is found. Testhook wants
                    ' to know if namespace can be found
                    ns = XNamespace.Get(NamespaceName)
                    NamespaceFound = ns IsNot Nothing
                Catch
                    ' ignore any exception coming from there
                End Try
                If element IsNot Nothing Then
                    ' If 'element' was found then set 'ns' to Nothing
                    ' This means 'ns' was get to support Testhook
                    ns = Nothing
                End If
            End If

            ' Get hold of VsShell service.
            Dim vsShell As IVsShell = TryCast(_data.Container.GetService(GetType(SVsShell)), IVsShell)
            If vsShell IsNot Nothing Then
                ' Make sure XSD designer package is loaded.
                Dim xsdDesignerPackageGuid As New Guid("20AAF8FA-14C0-4897-8CA0-4D861E2B1212")
                Dim package As IVsPackage = Nothing
                If VSErrorHandler.Succeeded(vsShell.LoadPackage(xsdDesignerPackageGuid, package)) Then
                    ' Get hold of IXmlSchemaDesignerService
                    Dim packageSP As IServiceProvider = TryCast(package, IServiceProvider)
                    If packageSP IsNot Nothing Then
                        Dim service As IXmlSchemaDesignerService = TryCast(
                            packageSP.GetService(GetType(IXmlSchemaDesignerService)),
                            IXmlSchemaDesignerService)
                        If service IsNot Nothing Then
                            ' Call the service to show the element or the namespace (whichever is not null).
                            If ns IsNot Nothing Then
                                service.AssociateSet(_data.SchemaSet, ns)
                            Else
                                service.AssociateSet(_data.SchemaSet, element)
                            End If
                        End If
                    End If
                End If
            End If
        End Sub
    End Class

    '--------------------------------------------------------------------------
    ' MemberSet:
    '   Defined for brevity.
    '--------------------------------------------------------------------------
    Friend Class MemberSet
        Inherits Dictionary(Of XmlIntellisenseMember, XmlIntellisenseMember)
    End Class

    '--------------------------------------------------------------------------
    ' FixupList:
    '   Defined for brevity.
    '--------------------------------------------------------------------------
    Friend Class FixupList
        Inherits List(Of KeyValuePair(Of XmlIntellisenseMember, XmlIntellisenseMember))
    End Class

    '--------------------------------------------------------------------------
    ' XmlIntellisenseMemberList:
    '   Class which represents a list of Xml intellisense member results, in
    '   the form of element and attribute declarations.
    '--------------------------------------------------------------------------
    <ClassInterface(ClassInterfaceType.None)>
    Friend Class XmlIntellisenseMember
        Implements IXmlIntellisenseMember

        Private ReadOnly _name As XmlQualifiedName
        Private _children As XmlIntellisenseMember
        Private _nextMember As XmlIntellisenseMember
        Private _flags As Flags
        Private ReadOnly _element As XmlSchemaElement

        Private Shared ReadOnly s_any As XmlIntellisenseMember

        Private Enum Flags
            None = 0
            IsElement = 1
            IsRoot = 2
        End Enum

        Shared Sub New()
            ' Construct content model for xs:any, where any attribute and any element is allowed as content
            s_any = AnyElement()
            s_any._nextMember = AnyAttribute()
            s_any._children = s_any
        End Sub

        Public Shared Function AnyElement() As XmlIntellisenseMember
            Dim Member As XmlIntellisenseMember = New XmlIntellisenseMember(XmlQualifiedName.Empty, Flags.IsElement) With {
                .Children = s_any
            }
            Return Member
        End Function

        Public Shared Function AnyAttribute() As XmlIntellisenseMember
            Return New XmlIntellisenseMember(XmlQualifiedName.Empty, Flags.None)
        End Function

        Private Sub New(Name As XmlQualifiedName, MemberFlags As Flags)
            _name = Name
            _flags = MemberFlags
        End Sub

        Public Sub New(Element As XmlSchemaElement)
            _name = Element.QualifiedName
            _flags = Flags.IsElement
            _element = Element
        End Sub

        Public Sub New(Attribute As XmlSchemaAttribute)
            _name = Attribute.QualifiedName
        End Sub

        Friend ReadOnly Property Element As XmlSchemaElement
            Get
                Return _element
            End Get
        End Property

        Public Property Children As XmlIntellisenseMember
            Get
                Return _children
            End Get
            Set
                _children = Value
            End Set
        End Property

        Public Property NextMember As XmlIntellisenseMember
            Get
                Return _nextMember
            End Get
            Set
                _nextMember = Value
            End Set
        End Property

        Public Property IsRoot As Boolean
            Get
                Return (_flags And Flags.IsRoot) <> 0
            End Get
            Set
                Debug.Assert(Value, "IsRoot can only be set to true")
                _flags = _flags Or Flags.IsRoot
            End Set
        End Property

        Public ReadOnly Property IsAny As Boolean
            Get
                Return _name.IsEmpty
            End Get
        End Property

        Public ReadOnly Property IsElement As Boolean _
            Implements IXmlIntellisenseMember.IsElement
            Get
                Return (_flags And Flags.IsElement) <> 0
            End Get
        End Property

        Public ReadOnly Property NamespaceName As String _
            Implements IXmlIntellisenseMember.NamespaceName
            Get
                Return _name.Namespace
            End Get
        End Property

        Public ReadOnly Property LocalName As String _
            Implements IXmlIntellisenseMember.LocalName
            Get
                Return _name.Name
            End Get
        End Property

        Public ReadOnly Property Name As XmlQualifiedName
            Get
                Return _name
            End Get
        End Property

    End Class

    '--------------------------------------------------------------------------
    ' IndexedMembers:
    '   Class which contains all element and attribute declarations discovered
    '   in a particular schema set, indexed by name and namespace for fast
    '   query access.
    '--------------------------------------------------------------------------
    <ClassInterface(ClassInterfaceType.None)>
    Friend Class IndexedMembers
        Private ReadOnly _targetNamespaces As Dictionary(Of String, String)
        Private ReadOnly _indexedByNamespace As Dictionary(Of String, List(Of XmlIntellisenseMember))
        Private ReadOnly _indexedByName As Dictionary(Of XmlQualifiedName, Object)
        Private ReadOnly _all As XmlIntellisenseMemberList
        Private ReadOnly _document As XmlIntellisenseMemberList
        Private ReadOnly _roots As XmlIntellisenseMemberList
        Private ReadOnly _elements As XmlIntellisenseMemberList

        Private Shared ReadOnly s_anyElement As XmlIntellisenseMember = XmlIntellisenseMember.AnyElement()
        Private Shared ReadOnly s_anyAttribute As XmlIntellisenseMember = XmlIntellisenseMember.AnyAttribute()

        Public Sub New(SchemaSet As XmlSchemaSet, TargetNamespaces As Dictionary(Of String, String))
            Dim TypeMap As Dictionary(Of XmlSchemaType, XmlIntellisenseMember) = New Dictionary(Of XmlSchemaType, XmlIntellisenseMember)()
            Dim ChildrenFixups As FixupList = New FixupList()
            Dim Roots As List(Of XmlIntellisenseMember) = New List(Of XmlIntellisenseMember)()

            _targetNamespaces = TargetNamespaces
            _indexedByNamespace = New Dictionary(Of String, List(Of XmlIntellisenseMember))()
            _indexedByName = New Dictionary(Of XmlQualifiedName, Object)()

            ' Start by adding all global element and attribute declarations to roots list
            For Each Element As XmlSchemaElement In SchemaSet.GlobalElements.Values
                Dim Root As XmlIntellisenseMember = CreateElementMember(Element, TypeMap, ChildrenFixups)
                Root.IsRoot = True
                Roots.Add(Root)
            Next

            ' AnyElement is always a root
            Roots.Add(s_anyElement)

            ' Apply fixups for recursively defined types
            For Each Fixup As KeyValuePair(Of XmlIntellisenseMember, XmlIntellisenseMember) In ChildrenFixups
                Fixup.Key.Children = Fixup.Value.Children
            Next

            _all = New XmlIntellisenseMemberList(Me)
            _document = New XmlIntellisenseMemberList(Me)
            _roots = New XmlIntellisenseMemberList(Me, Roots)
            _elements = New XmlIntellisenseMemberList(Me)

        End Sub

        Public ReadOnly Property All As XmlIntellisenseMemberList
            Get
                Return _all
            End Get
        End Property

        Public ReadOnly Property Document As XmlIntellisenseMemberList
            Get
                Return _document
            End Get
        End Property

        Public ReadOnly Property Roots As XmlIntellisenseMemberList
            Get
                Return _roots
            End Get
        End Property

        Public ReadOnly Property Elements As XmlIntellisenseMemberList
            Get
                Return _elements
            End Get
        End Property

        ' Returns true if the namespace is defined by some schema in the set, or there exists an element or attribute declaration
        ' that defines this name in some schema in the set
        Public Function IsNamespaceDefined(Name As XmlQualifiedName) As Boolean
            ' If the namespace is defined, then type information is available for the name
            If _targetNamespaces.ContainsKey(Name.Namespace) Then
                Return True
            End If

            ' If the namespace is empty, then type information is available for the name only if the name is defined
            Return Name.Namespace.Length = 0 AndAlso _indexedByName.ContainsKey(Name)
        End Function

        Public Function FindRootsByNamespace(NamespaceName As String, Results As List(Of XmlIntellisenseMember)) As IEnumerable(Of XmlIntellisenseMember)
            For Each Root As XmlIntellisenseMember In _roots
                If Root.NamespaceName = NamespaceName Then
                    Results.Add(Root)
                End If
            Next

            Results.Add(s_anyElement)

            Return Results
        End Function

        Public Function FindByNamespace(NamespaceName As String, Match As Predicate(Of XmlIntellisenseMember), Results As List(Of XmlIntellisenseMember)) As IEnumerable(Of XmlIntellisenseMember)
            Dim Members As List(Of XmlIntellisenseMember) = Nothing

            If _indexedByNamespace.TryGetValue(NamespaceName, Members) Then
                For Each Member As XmlIntellisenseMember In Members
                    If Match(Member) Then
                        Results.Add(Member)
                    End If
                Next
            End If

            AddAny(Match, Results)

            Return Results
        End Function

        Public Function FindByName(Name As XmlQualifiedName, Match As Predicate(Of XmlIntellisenseMember), Results As List(Of XmlIntellisenseMember)) As IEnumerable(Of XmlIntellisenseMember)
            Dim o As Object = Nothing
            Dim Member As XmlIntellisenseMember

            If _indexedByName.TryGetValue(Name, o) Then
                If TypeOf o Is XmlIntellisenseMember Then
                    Member = DirectCast(o, XmlIntellisenseMember)
                    If Match(Member) Then
                        Results.Add(Member)
                    End If
                Else
                    For Each ListMember As XmlIntellisenseMember In DirectCast(o, List(Of XmlIntellisenseMember))
                        If Match(ListMember) Then
                            Results.Add(ListMember)
                        End If
                    Next
                End If
            End If

            If Not IsNamespaceDefined(Name) Then
                AddAny(Match, Results)
            End If

            Return Results
        End Function

        Private Shared Sub AddAny(Match As Predicate(Of XmlIntellisenseMember), Results As List(Of XmlIntellisenseMember))
            ' FindByNamespace and FindByName should always find Any members, as long as types match
            If Match(s_anyElement) Then
                Results.Add(s_anyElement)
            End If

            If Match(s_anyAttribute) Then
                Results.Add(s_anyAttribute)
            End If
        End Sub

        Private Function CreateElementMember(Element As XmlSchemaElement, TypeMap As Dictionary(Of XmlSchemaType, XmlIntellisenseMember), ChildrenFixups As FixupList) As XmlIntellisenseMember
            Dim NewMember As XmlIntellisenseMember = New XmlIntellisenseMember(Element)
            Dim ExistingMember As XmlIntellisenseMember = Nothing

            ' If the element's type has complex content,
            Dim ContentType = TryCast(Element.ElementSchemaType, XmlSchemaComplexType)
            If ContentType IsNot Nothing Then

                ' Has type been traversed yet?
                If Not TypeMap.TryGetValue(ContentType, ExistingMember) Then
                    Dim ContentMembers As XmlIntellisenseMember = Nothing

                    ' No, so insert the type into the type map so that it can break cycles in the schema graph
                    TypeMap.Add(ContentType, NewMember)

                    ' Check for AnyAttribute property
                    If ContentType.AnyAttribute IsNot Nothing Then
                        ContentMembers = Concat(XmlIntellisenseMember.AnyAttribute(), ContentMembers)
                    End If

                    ' Yes, so create new content member linked list for the type
                    For Each Attribute As XmlSchemaAttribute In ContentType.AttributeUses.Values
                        ContentMembers = Concat(AddToIndexes(New XmlIntellisenseMember(Attribute)), ContentMembers)
                    Next

                    NewMember.Children = Concat(CreateContentMembers(ContentType.ContentTypeParticle, TypeMap, ChildrenFixups), ContentMembers)

                ElseIf ExistingMember.Children Is Nothing Then
                    ' Type is recursively defined, so fixup content later
                    ChildrenFixups.Add(New KeyValuePair(Of XmlIntellisenseMember, XmlIntellisenseMember)(NewMember, ExistingMember))
                Else
                    ' New element member has same children as existing element member
                    NewMember.Children = ExistingMember.Children
                End If
            End If

            ' Add element member to indexes
            Return AddToIndexes(NewMember)
        End Function

        Private Function CreateContentMembers(Particle As XmlSchemaParticle, TypeMap As Dictionary(Of XmlSchemaType, XmlIntellisenseMember), ChildrenFixups As FixupList) As XmlIntellisenseMember
            ' Check for element declaration particle first
            Dim xmlSchemaParticle = TryCast(Particle, XmlSchemaElement)
            If xmlSchemaParticle IsNot Nothing Then
                Return CreateElementMember(xmlSchemaParticle, TypeMap, ChildrenFixups)
            End If

            ' Recurse into particle group (choice, sequence, all)
            Dim xmlSchemaGroupBase = TryCast(Particle, XmlSchemaGroupBase)
            If xmlSchemaGroupBase IsNot Nothing Then
                Dim ContentMembers As XmlIntellisenseMember = Nothing

                For Each GroupParticle As XmlSchemaParticle In xmlSchemaGroupBase.Items
                    ContentMembers = Concat(CreateContentMembers(GroupParticle, TypeMap, ChildrenFixups), ContentMembers)
                Next

                Return ContentMembers
            End If

            ' Check for xs:any particle
            If TypeOf Particle Is XmlSchemaAny Then
                Return XmlIntellisenseMember.AnyElement()
            End If

            Return Nothing
        End Function

        Private Shared Function Concat(Left As XmlIntellisenseMember, Right As XmlIntellisenseMember) As XmlIntellisenseMember
            Dim Current As XmlIntellisenseMember = Left

            If Current Is Nothing Then
                Return Right
            End If

            While Current.NextMember IsNot Nothing
                Current = Current.NextMember
            End While

            Current.NextMember = Right
            Return Left
        End Function

        Private Function AddToIndexes(Member As XmlIntellisenseMember) As XmlIntellisenseMember
            Dim MemberList As List(Of XmlIntellisenseMember) = Nothing
            Dim ByName As Object = Nothing

            ' Add member to namespace index
            If Not _indexedByNamespace.TryGetValue(Member.NamespaceName, MemberList) Then
                MemberList = New List(Of XmlIntellisenseMember)()
                _indexedByNamespace(Member.NamespaceName) = MemberList
            End If
            MemberList.Add(Member)

            ' Add member to name index
            If Not _indexedByName.TryGetValue(Member.Name, ByName) Then
                _indexedByName(Member.Name) = Member
            Else
                If TypeOf ByName Is List(Of XmlIntellisenseMember) Then
                    MemberList = DirectCast(ByName, List(Of XmlIntellisenseMember))
                Else
                    MemberList = New List(Of XmlIntellisenseMember)()
                    _indexedByName(Member.Name) = MemberList
                    MemberList.Add(DirectCast(ByName, XmlIntellisenseMember))
                End If

                MemberList.Add(Member)
            End If

            Return Member
        End Function
    End Class

    '--------------------------------------------------------------------------
    ' XmlIntellisenseMemberList:
    '   Class which represents a list of Xml intellisense member results, in
    '   the form of element and attribute declarations.
    '
    ' The Any rules:
    '
    '   1. Pretend there is an "Any" schema in the set, which allows any element
    '      and any attribute at any position in a document.  This means that
    '      the set of Roots contains an Any member.
    '
    '   2. An axis having an "exact" name (i.e. containing both a namespace part
    '      and a local name part) never propagates Any members in its input
    '      set, except in the case where the name is "untyped" (i.e.
    '      IsNamespaceDefined returns False).
    '
    '   3. An axis having only a namespace part always propagates Any members
    '      in its input set.
    '--------------------------------------------------------------------------
    <ClassInterface(ClassInterfaceType.None)>
    Friend Class XmlIntellisenseMemberList
        Implements IXmlIntellisenseMemberList, IEnumerable(Of XmlIntellisenseMember)

        Private ReadOnly _allMembers As IndexedMembers
        Private ReadOnly _previousStep As XmlIntellisenseMemberList
        Private ReadOnly _axis As Axis
        Private ReadOnly _name As XmlQualifiedName
        Private _members As IEnumerable(Of XmlIntellisenseMember)

        Private Enum Axis
            Elements
            Attributes
            Descendants
        End Enum

        '--------------------------------------------------------------------------
        ' New:
        '   Initialize a list containing all possible declarations.
        '--------------------------------------------------------------------------
        Public Sub New(
            AllMembers As IndexedMembers
            )

            _allMembers = AllMembers
        End Sub

        '--------------------------------------------------------------------------
        ' New:
        '   Initialize a list containing the specified members.
        '--------------------------------------------------------------------------
        Public Sub New(
            AllMembers As IndexedMembers,
            Members As IEnumerable(Of XmlIntellisenseMember)
            )

            _allMembers = AllMembers
            _members = Members
        End Sub

        '--------------------------------------------------------------------------
        ' New:
        '   Initialize a list containing declarations found by starting with a
        '   previous list and following the specified axis from that point.
        '--------------------------------------------------------------------------
        Private Sub New(
            AllMembers As IndexedMembers,
            PreviousStep As XmlIntellisenseMemberList,
            AxisOfStep As Axis,
            NameOfStep As XmlQualifiedName
            )

            _allMembers = AllMembers
            _previousStep = PreviousStep
            _axis = AxisOfStep
            _name = NameOfStep
        End Sub

        '--------------------------------------------------------------------------
        ' MatchesNamedType:
        '   True if this list returns members that match a type defined in one of
        '   of the schemas in the set.  More than one type may be matched only if
        '   the other types share the same expanded name.
        '--------------------------------------------------------------------------
        Public ReadOnly Property MatchesNamedType As Boolean _
            Implements IXmlIntellisenseMemberList.MatchesNamedType

            Get
                Return _name IsNot Nothing AndAlso _name.Name.Length <> 0 AndAlso _allMembers.IsNamespaceDefined(_name)
            End Get
        End Property

        '--------------------------------------------------------------------------
        ' GetEnumerator:
        '   Get enumerator over the list of members.
        '--------------------------------------------------------------------------
        Public Function GetEnumerator() As IEnumerator(Of XmlIntellisenseMember) _
            Implements IEnumerable(Of XmlIntellisenseMember).GetEnumerator

            If _members Is Nothing Then
                Dim Results As List(Of XmlIntellisenseMember) = New List(Of XmlIntellisenseMember)()

                If _previousStep Is Nothing Then
                    Debug.Fail("GetEnumerator() should never be called on the Document or All member lists (it is not implemented).")
                ElseIf _previousStep Is _allMembers.All Then
                    ' Applying a query to the document root and all elements yields all matching members at any level
                    If _name.Name.Length = 0 Then
                        _allMembers.FindByNamespace(_name.Namespace, AddressOf MatchesType, Results)
                    Else
                        _allMembers.FindByName(_name, AddressOf MatchesType, Results)
                    End If
                ElseIf _previousStep Is _allMembers.Roots Then
                    ' Applying a descendant query to all roots yields all matching members at any level except the root level
                    If _axis = Axis.Descendants Then
                        If _name.Name.Length = 0 Then
                            _allMembers.FindByNamespace(_name.Namespace, AddressOf MatchesTypeAndNonRoot, Results)
                        Else
                            _allMembers.FindByName(_name, AddressOf MatchesType, Results)
                        End If
                    Else
                        FindChildMatches(_previousStep._members, False, Results)
                    End If
                ElseIf _previousStep Is _allMembers.Document Then
                    ' Applying a descendant query to the document member yields all matching members at any level
                    If _axis = Axis.Descendants Then
                        If _name.Name.Length = 0 Then
                            _allMembers.FindByNamespace(_name.Namespace, AddressOf MatchesType, Results)
                        Else
                            _allMembers.FindByName(_name, AddressOf MatchesType, Results)
                        End If
                    ElseIf _axis = Axis.Elements Then
                        If _name.Name.Length = 0 Then
                            _allMembers.FindRootsByNamespace(_name.Namespace, Results)
                        Else
                            _allMembers.FindByName(_name, AddressOf MatchesType, Results)
                        End If
                    End If
                ElseIf _previousStep Is _allMembers.Elements Then
                    ' Applying a query to all elements yields all matching members at any level except the root level
                    ' Note that refs to global elements are separate declarations, and will be found by MatchesTypeAndNonRoot
                    If _name.Name.Length = 0 Then
                        _allMembers.FindByNamespace(_name.Namespace, AddressOf MatchesTypeAndNonRoot, Results)
                    Else
                        _allMembers.FindByName(_name, AddressOf MatchesType, Results)
                    End If
                Else
                    FindChildMatches(_previousStep, _axis = Axis.Descendants, Results)
                End If

                _members = Results
            End If

            Return _members.GetEnumerator()
        End Function

        '--------------------------------------------------------------------------
        ' GetEnumerator:
        '   Get enumerator over the list of members.
        '--------------------------------------------------------------------------
        Public Function IEnumerable_GetEnumerator() As IEnumerator _
            Implements IEnumerable.GetEnumerator

            Return GetEnumerator()
        End Function

        '--------------------------------------------------------------------------
        ' Document:
        '   Get member list containing single "document" declaration which acts
        '   as the root of queries.
        '--------------------------------------------------------------------------
        Public Function Document() As IXmlIntellisenseMemberList _
            Implements IXmlIntellisenseMemberList.Document

            Return _allMembers.Document
        End Function

        '--------------------------------------------------------------------------
        ' Document:
        '   Get member list containing all element declarations.
        '--------------------------------------------------------------------------
        Public Function AllElements() As IXmlIntellisenseMemberList _
            Implements IXmlIntellisenseMemberList.AllElements

            Return _allMembers.Elements
        End Function

        '--------------------------------------------------------------------------
        ' Document:
        '   Get member list containing all global element declarations.
        '--------------------------------------------------------------------------
        Public Function GlobalElements() As IXmlIntellisenseMemberList _
            Implements IXmlIntellisenseMemberList.GlobalElements

            Return _allMembers.Roots
        End Function

        '--------------------------------------------------------------------------
        ' All:
        '   Get member list containing union of Document() and AllElements().
        '--------------------------------------------------------------------------
        Public Function All() As IXmlIntellisenseMemberList _
            Implements IXmlIntellisenseMemberList.All

            Return _allMembers.All
        End Function

        '--------------------------------------------------------------------------
        ' ElementsByNamespace:
        '   Get child elements of this list having the specified namespace.
        '--------------------------------------------------------------------------
        Public Function ElementsByNamespace(
            <[In], MarshalAs(UnmanagedType.BStr)> NamespaceName As String
            ) As IXmlIntellisenseMemberList _
            Implements IXmlIntellisenseMemberList.ElementsByNamespace

            Return New XmlIntellisenseMemberList(_allMembers, Me, Axis.Elements, New XmlQualifiedName(String.Empty, NamespaceName))
        End Function

        '--------------------------------------------------------------------------
        ' ElementsByName:
        '   Get child elements of this list having the specified name.
        '--------------------------------------------------------------------------
        Public Function ElementsByName(
            <[In], MarshalAs(UnmanagedType.BStr)> NamespaceName As String,
            <[In], MarshalAs(UnmanagedType.BStr)> LocalName As String
            ) As IXmlIntellisenseMemberList _
            Implements IXmlIntellisenseMemberList.ElementsByName

            Return New XmlIntellisenseMemberList(_allMembers, Me, Axis.Elements, New XmlQualifiedName(LocalName, NamespaceName))
        End Function

        '--------------------------------------------------------------------------
        ' AttributesByNamespace:
        '   Get attributes of this list having the specified namespace.
        '--------------------------------------------------------------------------
        Public Function AttributesByNamespace(
            <[In], MarshalAs(UnmanagedType.BStr)> NamespaceName As String
            ) As IXmlIntellisenseMemberList _
            Implements IXmlIntellisenseMemberList.AttributesByNamespace

            Return New XmlIntellisenseMemberList(_allMembers, Me, Axis.Attributes, New XmlQualifiedName(String.Empty, NamespaceName))
        End Function

        '--------------------------------------------------------------------------
        ' AttributesByName:
        '   Get attributes of this list having the specified name.
        '--------------------------------------------------------------------------
        Public Function AttributesByName(
            <[In], MarshalAs(UnmanagedType.BStr)> NamespaceName As String,
            <[In], MarshalAs(UnmanagedType.BStr)> LocalName As String
            ) As IXmlIntellisenseMemberList _
            Implements IXmlIntellisenseMemberList.AttributesByName

            Return New XmlIntellisenseMemberList(_allMembers, Me, Axis.Attributes, New XmlQualifiedName(LocalName, NamespaceName))
        End Function

        '--------------------------------------------------------------------------
        ' DescendantsByNamespace:
        '   Get descendant elements of this list having the specified namespace.
        '--------------------------------------------------------------------------
        Public Function DescendantsByNamespace(
            <[In], MarshalAs(UnmanagedType.BStr)> NamespaceName As String
            ) As IXmlIntellisenseMemberList _
            Implements IXmlIntellisenseMemberList.DescendantsByNamespace

            Return New XmlIntellisenseMemberList(_allMembers, Me, Axis.Descendants, New XmlQualifiedName(String.Empty, NamespaceName))
        End Function

        '--------------------------------------------------------------------------
        ' DescendantsByName:
        '   Get descendant elements of this list having the specified name.
        '--------------------------------------------------------------------------
        Public Function DescendantsByName(
            <[In], MarshalAs(UnmanagedType.BStr)> NamespaceName As String,
            <[In], MarshalAs(UnmanagedType.BStr)> LocalName As String
            ) As IXmlIntellisenseMemberList _
            Implements IXmlIntellisenseMemberList.DescendantsByName

            Return New XmlIntellisenseMemberList(_allMembers, Me, Axis.Descendants, New XmlQualifiedName(LocalName, NamespaceName))
        End Function

        '--------------------------------------------------------------------------
        ' GetEnumerator:
        '   Get an IXmlIntellisenseMemberEnumerator, which is more easily
        '   consumed by native code than IEnumerable/IEnumerator.
        '--------------------------------------------------------------------------
        Public Function IXmlIntellisenseMemberList_GetEnumerator() As IXmlIntellisenseMemberEnumerator _
            Implements IXmlIntellisenseMemberList.GetEnumerator

            Return New XmlIntellisenseMemberEnumerator(GetEnumerator())
        End Function


        '--------------------------------------------------------------------------
        ' MatchesType:
        '   Returns True if the specified member's axis matches this list's axis.
        '--------------------------------------------------------------------------
        Private Function MatchesType(Member As XmlIntellisenseMember) As Boolean
            ' Attribute axis matches only attribute members, element/descendant axes only match element members
            Return Member.IsElement = (_axis <> Axis.Attributes)
        End Function

        '--------------------------------------------------------------------------
        ' MatchesTypeAndName:
        '   Returns True if the specified member's axis matches this list's axis,
        '   and the member's name matches this list's name.
        '--------------------------------------------------------------------------
        Private Function MatchesTypeAndName(Member As XmlIntellisenseMember) As Boolean
            ' Check type
            If Not MatchesType(Member) Then
                Return False
            End If

            ' xs:any matches any name
            If Member.IsAny Then
                Return True
            End If

            ' Check name
            Dim LocalName As String = _name.Name

            If LocalName.Length <> 0 AndAlso LocalName <> Member.LocalName Then
                Return False
            End If

            If _name.Namespace <> Member.NamespaceName Then
                Return False
            End If

            Return True
        End Function

        '--------------------------------------------------------------------------
        ' MatchesTypeAndNonRoot:
        '   Returns True if the specified member's axis matches this list's axis,
        '   and the member is not a global element or attribute declaration.
        '--------------------------------------------------------------------------
        Private Function MatchesTypeAndNonRoot(Member As XmlIntellisenseMember) As Boolean
            If Not MatchesType(Member) Then
                Return False
            End If

            Return Not Member.IsRoot
        End Function

        '--------------------------------------------------------------------------
        ' FindChildMatches:
        '   Iterate through the list of roots and find all matches.
        '--------------------------------------------------------------------------
        Private Function FindChildMatches(Roots As IEnumerable(Of XmlIntellisenseMember), Recurse As Boolean, Matches As List(Of XmlIntellisenseMember)) As IEnumerable(Of XmlIntellisenseMember)
            Dim CheckUnique As MemberSet = New MemberSet()

            For Each Root As XmlIntellisenseMember In Roots
                FindChildMatches(Root, Recurse, Matches, CheckUnique)
            Next

            Return Matches
        End Function

        Private Sub FindChildMatches(Root As XmlIntellisenseMember, Recurse As Boolean, Matches As List(Of XmlIntellisenseMember), CheckUnique As MemberSet)
            Dim Member As XmlIntellisenseMember = Root.Children

            If Member IsNot Nothing Then
                ' Insert first member into "checkUnique" set to ensure we don't cycle forever, and to ensure we don't add duplicate matches
                If Not AddUnique(Member, CheckUnique) Then
                    Return
                End If

                Do
                    ' If we found a match, add it to "matches" list
                    If MatchesTypeAndName(Member) Then
                        ' An exact name should match an xs:any particle only if it matches the name of an element declaration in some schema
                        If Member.IsAny AndAlso _name.Name.Length <> 0 AndAlso _allMembers.IsNamespaceDefined(_name) Then
                            ' Add all element declarations which match the name (will be empty set if none do, and no matches will be added)
                            Dim Results As List(Of XmlIntellisenseMember) = New List(Of XmlIntellisenseMember)

                            For Each Match As XmlIntellisenseMember In _allMembers.FindByName(_name, AddressOf MatchesType, Results)
                                If AddUnique(Match, CheckUnique) Then
                                    Matches.Add(Match)
                                End If
                            Next
                        Else
                            Matches.Add(Member)
                        End If
                    End If

                    ' Recurse()
                    If Recurse Then
                        FindChildMatches(Member, True, Matches, CheckUnique)
                    End If

                    ' Move to next child
                    Member = Member.NextMember

                Loop While Member IsNot Nothing
            End If
        End Sub

        Private Shared Function AddUnique(Member As XmlIntellisenseMember, CheckUnique As MemberSet) As Boolean
            If CheckUnique.ContainsKey(Member) Then
                Return False
            End If

            CheckUnique(Member) = Member
            Return True
        End Function

    End Class

    <ClassInterface(ClassInterfaceType.None)>
    Friend Class XmlIntellisenseMemberEnumerator
        Implements IXmlIntellisenseMemberEnumerator

        Private ReadOnly _enumerator As IEnumerator(Of XmlIntellisenseMember)

        Public Sub New(Enumerator As IEnumerator(Of XmlIntellisenseMember))
            _enumerator = Enumerator
        End Sub

        Public Function GetNext() As IXmlIntellisenseMember _
            Implements IXmlIntellisenseMemberEnumerator.GetNext

            If Not _enumerator.MoveNext() Then
                Return Nothing
            End If

            Return _enumerator.Current
        End Function

    End Class

End Namespace
