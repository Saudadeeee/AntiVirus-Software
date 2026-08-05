Imports System.IO
Imports System.Threading
Imports System.Reflection
Imports System.Runtime.Loader
Imports AVAK.Core
Imports AVAK.Security

Namespace Engine

    Public Class ProviderState
        Public Property Provider As IDetectionProvider
        Public Property Enabled As Boolean = True
        Public Property Source As String = "built-in"
        Public Property Failures As Integer = 0
        Public Property LastError As String = ""
        ''' <summary>Fields, not properties: Interlocked needs ByRef access.</summary>
        Public Detections As Long = 0
        Public ElapsedMs As Long = 0

        Public ReadOnly Property Healthy As Boolean
            Get
                Return Failures < MaxFailures
            End Get
        End Property

        Public Const MaxFailures As Integer = 5
    End Class

    ''' <summary>
    ''' Discovers and owns every detection provider.
    '''
    ''' Built-ins are registered in code. Third-party providers are loaded from the
    ''' <c>plugins</c> folder next to the executable: every *.dll there is inspected
    ''' for public types implementing <see cref="IDetectionProvider"/>.
    '''
    ''' A provider that throws repeatedly is disabled rather than allowed to break
    ''' the scan; the reason is shown on the Rules page.
    ''' </summary>
    Public NotInheritable Class ProviderRegistry

        Private Sub New()
        End Sub

        Private Shared ReadOnly Gate As New Object()
        Private Shared _states As List(Of ProviderState)

        Public Shared Event Changed As EventHandler

        Public Shared ReadOnly Property PluginFolder As String
            Get
                Dim p = Path.Combine(AppPaths.AppDir, "plugins")
                Try
                    If Not Directory.Exists(p) Then Directory.CreateDirectory(p)
                Catch
                End Try
                Return p
            End Get
        End Property

        Public Shared Function All() As List(Of ProviderState)
            EnsureLoaded()
            SyncLock Gate
                Return _states.ToList()
            End SyncLock
        End Function

        ''' <summary>Providers that will actually run, in execution order.</summary>
        Public Shared Function Active() As List(Of IDetectionProvider)
            EnsureLoaded()
            SyncLock Gate
                Return _states.Where(Function(s) s.Enabled AndAlso s.Healthy).
                               OrderBy(Function(s) s.Provider.Order).
                               Select(Function(s) s.Provider).ToList()
            End SyncLock
        End Function

        Public Shared Sub SetEnabled(id As String, enabled As Boolean)
            SyncLock Gate
                Dim s = _states?.FirstOrDefault(Function(x) x.Provider.Id = id)
                If s Is Nothing Then Return
                s.Enabled = enabled
                s.Failures = 0
            End SyncLock

            Dim cfg = AppSettings.Current
            cfg.DisabledProviders.RemoveAll(Function(x) String.Equals(x, id, StringComparison.OrdinalIgnoreCase))
            If Not enabled Then cfg.DisabledProviders.Add(id)
            cfg.Save()

            RaiseEvent Changed(Nothing, EventArgs.Empty)
        End Sub

        Public Shared Sub EnsureLoaded()
            SyncLock Gate
                If _states IsNot Nothing Then Return
                _states = New List(Of ProviderState)()

                For Each p In BuiltIns()
                    Add(p, "built-in")
                Next

                LoadPlugins()

                Dim disabled = AppSettings.Current.DisabledProviders
                For Each s In _states
                    If disabled.Any(Function(d) String.Equals(d, s.Provider.Id, StringComparison.OrdinalIgnoreCase)) Then
                        s.Enabled = False
                    End If
                Next

                Logger.Info($"Providers: {_states.Where(Function(s) s.Enabled).Count()} active of {_states.Count}")
            End SyncLock
        End Sub

        Public Shared Sub Reload()
            SyncLock Gate
                _states = Nothing
            End SyncLock
            EnsureLoaded()
            RaiseEvent Changed(Nothing, EventArgs.Empty)
        End Sub

        Private Shared Function BuiltIns() As IEnumerable(Of IDetectionProvider)
            Return New IDetectionProvider() {
                New Providers.HashProvider(),
                New Providers.PatternProvider(),
                New Providers.ArchiveProvider(),
                New Providers.HeuristicProvider()
            }
        End Function

        Private Shared Sub Add(p As IDetectionProvider, source As String)
            Try
                If _states.Any(Function(s) String.Equals(s.Provider.Id, p.Id, StringComparison.OrdinalIgnoreCase)) Then
                    Logger.Warn($"Duplicate provider id '{p.Id}' from {source} - ignored")
                    Return
                End If
                If Not p.Initialise() Then
                    Logger.Info($"Provider '{p.Id}' opted out during initialisation")
                    Return
                End If
                _states.Add(New ProviderState With {.Provider = p, .Source = source})
            Catch ex As Exception
                Logger.Error($"Provider '{If(p?.Id, "?")}' failed to initialise", ex)
            End Try
        End Sub

        Private Shared Sub LoadPlugins()
            Dim folder = PluginFolder
            Dim files As String()
            Try
                files = Directory.GetFiles(folder, "*.dll")
            Catch
                Return
            End Try

            For Each dll In files
                Try
                    ' Load into the default context so the plugin shares AVAK's own
                    ' types. Isolation would mean the interface types do not match.
                    Dim asm = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.GetFullPath(dll))
                    Dim found = 0

                    For Each t In asm.GetTypes()
                        If t.IsAbstract OrElse t.IsInterface OrElse Not t.IsPublic Then Continue For
                        If Not GetType(IDetectionProvider).IsAssignableFrom(t) Then Continue For
                        If t.GetConstructor(Type.EmptyTypes) Is Nothing Then
                            Logger.Warn($"Plugin type {t.FullName} has no parameterless constructor - skipped")
                            Continue For
                        End If

                        Dim instance = TryCast(Activator.CreateInstance(t), IDetectionProvider)
                        If instance Is Nothing Then Continue For
                        Add(instance, Path.GetFileName(dll))
                        found += 1
                    Next

                    If found = 0 Then
                        Logger.Warn($"Plugin '{Path.GetFileName(dll)}' contains no detection provider")
                    Else
                        Logger.Info($"Plugin '{Path.GetFileName(dll)}' contributed {found} provider(s)")
                    End If

                Catch ex As ReflectionTypeLoadException
                    Logger.Error($"Plugin '{Path.GetFileName(dll)}' could not be inspected", ex)
                Catch ex As Exception
                    Logger.Error($"Plugin '{Path.GetFileName(dll)}' failed to load", ex)
                End Try
            Next
        End Sub

        ''' <summary>
        ''' Runs every active provider over one file and returns the first terminal
        ''' detection, or the highest-severity non-terminal one.
        ''' </summary>
        Public Shared Function Inspect(context As DetectionContext) As Detection
            EnsureLoaded()

            Dim states As List(Of ProviderState)
            SyncLock Gate
                states = _states.Where(Function(s) s.Enabled AndAlso s.Healthy).
                                 OrderBy(Function(s) s.Provider.Order).ToList()
            End SyncLock

            Dim best As Detection = Nothing

            For Each state In states
                Dim sw = Stopwatch.StartNew()
                Try
                    If Not state.Provider.CanHandle(context) Then Continue For

                    Dim hit = state.Provider.Inspect(context)
                    If hit Is Nothing Then Continue For

                    Interlocked.Increment(state.Detections)
                    If state.Provider.Terminal Then Return hit
                    If best Is Nothing OrElse hit.Severity > best.Severity Then best = hit

                Catch ex As Exception
                    state.Failures += 1
                    state.LastError = ex.Message
                    Logger.Warn($"Provider '{state.Provider.Id}' threw on {context.FilePath}: {ex.Message}")
                    If Not state.Healthy Then
                        Logger.Error($"Provider '{state.Provider.Id}' disabled after {ProviderState.MaxFailures} failures")
                    End If
                Finally
                    sw.Stop()
                    Interlocked.Add(state.ElapsedMs, sw.ElapsedMilliseconds)
                End Try
            Next

            Return best
        End Function

        Public Shared Sub ResetStats()
            SyncLock Gate
                If _states Is Nothing Then Return
                For Each s In _states
                    s.Detections = 0
                    s.ElapsedMs = 0
                    s.Failures = 0
                    s.LastError = ""
                Next
            End SyncLock
        End Sub

    End Class

End Namespace
