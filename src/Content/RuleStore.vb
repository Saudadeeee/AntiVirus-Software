Imports System.IO
Imports AVAK.Core
Imports AVAK.Security

Namespace Content

    Public Class HashMatch
        Public Property Signature As HashSignature
        Public Property PackName As String = ""
    End Class

    Public Class PatternMatch
        Public Property Pattern As SignatureDatabase.CompiledPattern
        Public Property PackName As String = ""
    End Class

    ''' <summary>
    ''' Owns every rule pack: discovery, indexing, enable/disable, editing and
    ''' hot reload. This is the single place rules come from - providers ask the
    ''' store, never a file.
    '''
    ''' Folders:
    '''   &lt;app&gt;\content\rules\*.json           shipped defaults, read-only
    '''   %LOCALAPPDATA%\AVAK\rules\*.json      your packs, writable
    ''' </summary>
    Public NotInheritable Class RuleStore

        Private Sub New()
        End Sub

        Public Const PackExtension As String = ".rules.json"

        Private Shared ReadOnly Gate As New Object()
        Private Shared _packs As List(Of RulePack)
        Private Shared _bySha As Dictionary(Of String, HashMatch)
        Private Shared _byMd5 As Dictionary(Of String, HashMatch)
        Private Shared _patterns As List(Of PatternMatch)
        Private Shared _hasMd5 As Boolean

        Public Shared Event Changed As EventHandler

        ' -- folders -----------------------------------------------------------

        Public Shared ReadOnly Property BuiltInFolder As String
            Get
                Return Path.Combine(AppPaths.AppDir, "content", "rules")
            End Get
        End Property

        Public Shared ReadOnly Property UserFolder As String
            Get
                Dim p = Path.Combine(AppPaths.Root, "rules")
                Try
                    If Not Directory.Exists(p) Then Directory.CreateDirectory(p)
                Catch
                End Try
                Return p
            End Get
        End Property

        ' -- loading -----------------------------------------------------------

        Public Shared Sub EnsureLoaded()
            SyncLock Gate
                If _packs IsNot Nothing Then Return
                LoadLocked()
            End SyncLock
        End Sub

        Public Shared Sub Reload()
            SyncLock Gate
                LoadLocked()
            End SyncLock
            RaiseEvent Changed(Nothing, EventArgs.Empty)
        End Sub

        Private Shared Sub LoadLocked()
            _packs = New List(Of RulePack)()
            _bySha = New Dictionary(Of String, HashMatch)(StringComparer.OrdinalIgnoreCase)
            _byMd5 = New Dictionary(Of String, HashMatch)(StringComparer.OrdinalIgnoreCase)
            _patterns = New List(Of PatternMatch)()
            _hasMd5 = False

            Dim disabled = AppSettings.Current.DisabledRulePacks

            LoadFolder(BuiltInFolder, True, disabled)
            LoadFolder(UserFolder, False, disabled)

            ' legacy single-file database from before rule packs existed
            MigrateLegacy(disabled)

            For Each pack In _packs.Where(Function(p) p.Enabled)
                Index(pack)
            Next

            Logger.Info($"Rules: {_packs.Count} pack(s), {_bySha.Count} sha256, {_byMd5.Count} md5, {_patterns.Count} patterns")
        End Sub

        Private Shared Sub LoadFolder(folder As String, readOnlyPacks As Boolean, disabled As List(Of String))
            Dim files As String()
            Try
                If Not Directory.Exists(folder) Then Return
                files = Directory.GetFiles(folder, "*.json")
            Catch
                Return
            End Try

            For Each file In files.OrderBy(Function(f) f, StringComparer.OrdinalIgnoreCase)
                Dim pack = Json.Load(Of RulePack)(file)
                If pack Is Nothing Then
                    _packs.Add(New RulePack With {
                        .Name = Path.GetFileName(file),
                        .FilePath = file,
                        .ReadOnlyPack = readOnlyPacks,
                        .Enabled = False,
                        .Issues = New List(Of String) From {"The file is not valid JSON and was skipped."}})
                    Continue For
                End If

                pack.FilePath = file
                pack.ReadOnlyPack = readOnlyPacks
                pack.Normalise()
                pack.Issues = pack.Validate()
                pack.Enabled = Not disabled.Any(
                    Function(d) String.Equals(d, Path.GetFileName(file), StringComparison.OrdinalIgnoreCase))
                _packs.Add(pack)
            Next
        End Sub

        ''' <summary>Moves a pre-rule-pack data\signatures.json into the user rules folder once.</summary>
        Private Shared Sub MigrateLegacy(disabled As List(Of String))
            Try
                Dim legacy = AppPaths.UserSignatures
                If Not File.Exists(legacy) Then Return

                Dim target = Path.Combine(UserFolder, "migrated" & PackExtension)
                If Not File.Exists(target) Then
                    File.Copy(legacy, target)
                    Logger.Info("Migrated the legacy signature file into a rule pack")
                    Dim pack = Json.Load(Of RulePack)(target)
                    If pack IsNot Nothing Then
                        pack.FilePath = target
                        pack.Normalise()
                        If String.IsNullOrWhiteSpace(pack.Name) OrElse pack.Name = "migrated" & PackExtension Then
                            pack.Name = "Migrated signatures"
                        End If
                        pack.Issues = pack.Validate()
                        pack.Enabled = Not disabled.Any(
                            Function(d) String.Equals(d, Path.GetFileName(target), StringComparison.OrdinalIgnoreCase))
                        _packs.Add(pack)
                    End If
                End If
                File.Delete(legacy)
            Catch ex As Exception
                Logger.Warn("Legacy signature migration failed: " & ex.Message)
            End Try
        End Sub

        Private Shared Sub Index(pack As RulePack)
            For Each h In pack.Hashes
                Dim entry As New HashMatch With {.Signature = h, .PackName = pack.Name}
                If Not String.IsNullOrWhiteSpace(h.Sha256) Then _bySha(h.Sha256.Trim()) = entry
                If Not String.IsNullOrWhiteSpace(h.Md5) Then
                    _byMd5(h.Md5.Trim()) = entry
                    _hasMd5 = True
                End If
            Next

            For Each p In pack.Patterns
                Dim compiled = SignatureDatabase.Compile(p)
                If compiled Is Nothing Then Continue For
                _patterns.Add(New PatternMatch With {.Pattern = compiled, .PackName = pack.Name})
            Next
        End Sub

        ' -- queries -----------------------------------------------------------

        Public Shared Function Packs() As List(Of RulePack)
            EnsureLoaded()
            SyncLock Gate
                Return _packs.ToList()
            End SyncLock
        End Function

        Public Shared ReadOnly Property HasMd5Signatures As Boolean
            Get
                EnsureLoaded()
                Return _hasMd5
            End Get
        End Property

        Public Shared ReadOnly Property HashCount As Integer
            Get
                EnsureLoaded()
                SyncLock Gate
                    Return _bySha.Count + _byMd5.Count
                End SyncLock
            End Get
        End Property

        Public Shared ReadOnly Property PatternCount As Integer
            Get
                EnsureLoaded()
                SyncLock Gate
                    Return _patterns.Count
                End SyncLock
            End Get
        End Property

        Public Shared ReadOnly Property Version As String
            Get
                EnsureLoaded()
                SyncLock Gate
                    Dim enabled = _packs.Where(Function(p) p.Enabled).ToList()
                    If enabled.Count = 0 Then Return "none"
                    If enabled.Count = 1 Then Return enabled(0).Version
                    Return enabled.Count & " packs"
                End SyncLock
            End Get
        End Property

        Public Shared Function MatchHash(sha256 As String, md5 As String) As HashMatch
            EnsureLoaded()
            SyncLock Gate
                Dim m As HashMatch = Nothing
                If Not String.IsNullOrEmpty(sha256) AndAlso _bySha.TryGetValue(sha256, m) Then Return m
                If Not String.IsNullOrEmpty(md5) AndAlso _byMd5.TryGetValue(md5, m) Then Return m
                Return Nothing
            End SyncLock
        End Function

        Public Shared Function MatchPatterns(buffer As Byte(), length As Integer, extension As String) As List(Of PatternMatch)
            EnsureLoaded()
            Dim hits As New List(Of PatternMatch)()
            If buffer Is Nothing OrElse length <= 0 Then Return hits

            Dim snapshot As List(Of PatternMatch)
            SyncLock Gate
                snapshot = _patterns
            End SyncLock

            Dim ext = If(extension, "").ToLowerInvariant()
            For Each pm In snapshot
                Dim p = pm.Pattern
                If p.Extensions.Count > 0 AndAlso Not p.Extensions.Contains(ext) Then Continue For
                Dim limit = If(p.MaxOffset > 0, Math.Min(length, p.MaxOffset), length)
                If SignatureDatabase.IndexOfPattern(buffer, limit, p) >= 0 Then hits.Add(pm)
            Next
            Return hits
        End Function

        ' -- editing -----------------------------------------------------------

        Public Shared Sub SetPackEnabled(fileName As String, enabled As Boolean)
            Dim cfg = AppSettings.Current
            cfg.DisabledRulePacks.RemoveAll(Function(x) String.Equals(x, fileName, StringComparison.OrdinalIgnoreCase))
            If Not enabled Then cfg.DisabledRulePacks.Add(fileName)
            cfg.Save()
            Reload()
        End Sub

        ''' <summary>Creates an empty writable pack and returns its path.</summary>
        Public Shared Function CreatePack(name As String, Optional author As String = "") As String
            Dim safeName = SanitiseFileName(If(String.IsNullOrWhiteSpace(name), "custom", name))
            Dim target = Path.Combine(UserFolder, safeName & PackExtension)
            Dim n = 2
            While File.Exists(target)
                target = Path.Combine(UserFolder, safeName & "-" & n & PackExtension)
                n += 1
            End While

            Dim pack = RulePack.CreateTemplate(If(String.IsNullOrWhiteSpace(name), "Custom rules", name), author)
            Json.Save(target, pack)
            Reload()
            Logger.Info("Created rule pack " & target)
            Return target
        End Function

        ''' <summary>The pack new rules go into when the caller does not name one.</summary>
        Public Shared Function DefaultUserPack() As String
            Dim defaultPath = Path.Combine(UserFolder, "my-rules" & PackExtension)
            If Not File.Exists(defaultPath) Then
                Json.Save(defaultPath, RulePack.CreateTemplate("My rules", Environment.UserName))
                Reload()
            End If
            Return defaultPath
        End Function

        Public Class EditResult
            Public Property Ok As Boolean
            Public Property Message As String = ""
            Public Property PackPath As String = ""
        End Class

        Public Shared Function AddHash(sha256 As String, name As String, severity As Severity,
                                       Optional md5 As String = "",
                                       Optional packPath As String = Nothing) As EditResult
            If String.IsNullOrWhiteSpace(sha256) AndAlso String.IsNullOrWhiteSpace(md5) Then
                Return New EditResult With {.Ok = False, .Message = "A SHA-256 or MD5 value is required."}
            End If

            Dim target = If(String.IsNullOrWhiteSpace(packPath), DefaultUserPack(), packPath)
            Dim pack = Json.Load(Of RulePack)(target)
            If pack Is Nothing Then Return New EditResult With {.Ok = False, .Message = "The pack could not be read."}
            pack.Normalise()

            If pack.Hashes.Any(Function(h) String.Equals(h.Sha256, sha256, StringComparison.OrdinalIgnoreCase) AndAlso
                                           Not String.IsNullOrWhiteSpace(sha256)) Then
                Return New EditResult With {.Ok = False, .Message = "That hash is already in this pack.", .PackPath = target}
            End If

            pack.Hashes.Add(New HashSignature With {
                .Sha256 = If(sha256, "").Trim(),
                .Md5 = If(md5, "").Trim(),
                .Name = If(String.IsNullOrWhiteSpace(name), "Custom.Hash", name.Trim()),
                .Severity = severity})
            pack.Updated = DateTime.Now

            If Not Json.Save(target, pack) Then
                Return New EditResult With {.Ok = False, .Message = "The pack could not be written."}
            End If
            Reload()
            Return New EditResult With {.Ok = True, .PackPath = target,
                                        .Message = $"Added '{name}' to {Path.GetFileName(target)}."}
        End Function

        Public Shared Function AddPattern(pattern As PatternSignature,
                                          Optional packPath As String = Nothing) As EditResult
            If pattern Is Nothing Then Return New EditResult With {.Ok = False, .Message = "No pattern supplied."}

            Dim probe As New RulePack()
            probe.Patterns.Add(pattern)
            Dim problems = probe.Validate().Where(Function(x) Not x.Contains("no rules")).ToList()
            If problems.Count > 0 Then
                Return New EditResult With {.Ok = False, .Message = String.Join(Environment.NewLine, problems)}
            End If

            Dim target = If(String.IsNullOrWhiteSpace(packPath), DefaultUserPack(), packPath)
            Dim pack = Json.Load(Of RulePack)(target)
            If pack Is Nothing Then Return New EditResult With {.Ok = False, .Message = "The pack could not be read."}
            pack.Normalise()

            pack.Patterns.Add(pattern)
            pack.Updated = DateTime.Now

            If Not Json.Save(target, pack) Then
                Return New EditResult With {.Ok = False, .Message = "The pack could not be written."}
            End If
            Reload()
            Return New EditResult With {.Ok = True, .PackPath = target,
                                        .Message = $"Added pattern '{pattern.Name}' to {Path.GetFileName(target)}."}
        End Function

        Public Shared Function RemoveRule(packPath As String, ruleName As String) As EditResult
            Dim pack = Json.Load(Of RulePack)(packPath)
            If pack Is Nothing Then Return New EditResult With {.Ok = False, .Message = "The pack could not be read."}
            pack.Normalise()

            Dim removed = pack.Hashes.RemoveAll(Function(h) String.Equals(h.Name, ruleName, StringComparison.OrdinalIgnoreCase))
            removed += pack.Patterns.RemoveAll(Function(p) String.Equals(p.Name, ruleName, StringComparison.OrdinalIgnoreCase))
            If removed = 0 Then Return New EditResult With {.Ok = False, .Message = "No rule with that name in this pack."}

            pack.Updated = DateTime.Now
            Json.Save(packPath, pack)
            Reload()
            Return New EditResult With {.Ok = True, .Message = $"Removed {removed} rule(s)."}
        End Function

        ''' <summary>Copies an external pack into the user folder after validating it.</summary>
        Public Shared Function ImportPack(sourcePath As String) As EditResult
            Try
                Dim pack = Json.Load(Of RulePack)(sourcePath)
                If pack Is Nothing Then
                    Return New EditResult With {.Ok = False, .Message = "That file is not a valid rule pack."}
                End If
                pack.Normalise()
                Dim problems = pack.Validate()
                If problems.Any(Function(p) Not p.Contains("no rules")) Then
                    Return New EditResult With {.Ok = False,
                        .Message = "The pack has problems:" & Environment.NewLine &
                                   String.Join(Environment.NewLine, problems.Take(6))}
                End If

                Dim baseName = Path.GetFileNameWithoutExtension(sourcePath).Replace(".rules", "")
                Dim target = Path.Combine(UserFolder, SanitiseFileName(baseName) & PackExtension)
                Dim n = 2
                While File.Exists(target)
                    target = Path.Combine(UserFolder, SanitiseFileName(baseName) & "-" & n & PackExtension)
                    n += 1
                End While

                File.Copy(sourcePath, target)
                Reload()
                Return New EditResult With {.Ok = True, .PackPath = target,
                    .Message = $"Imported {pack.RuleCount} rule(s) as {Path.GetFileName(target)}."}
            Catch ex As Exception
                Return New EditResult With {.Ok = False, .Message = "Import failed: " & ex.Message}
            End Try
        End Function

        Public Shared Function DeletePack(packPath As String) As EditResult
            Try
                Dim pack = Packs().FirstOrDefault(Function(p) String.Equals(p.FilePath, packPath, StringComparison.OrdinalIgnoreCase))
                If pack IsNot Nothing AndAlso pack.ReadOnlyPack Then
                    Return New EditResult With {.Ok = False, .Message = "Shipped packs cannot be deleted - disable it instead."}
                End If
                File.Delete(packPath)
                Reload()
                Return New EditResult With {.Ok = True, .Message = "Pack deleted."}
            Catch ex As Exception
                Return New EditResult With {.Ok = False, .Message = ex.Message}
            End Try
        End Function

        Private Shared Function SanitiseFileName(s As String) As String
            Dim bad = Path.GetInvalidFileNameChars()
            Dim cleaned = New String(s.Select(Function(c) If(bad.Contains(c) OrElse c = " "c, "-"c, c)).ToArray())
            Return cleaned.Trim("-"c).ToLowerInvariant()
        End Function

    End Class

End Namespace
