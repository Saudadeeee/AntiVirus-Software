Imports System.Threading
Imports System.Windows.Forms
Imports AVAK.Core
Imports AVAK.Security

''' <summary>
''' Entry point. Keeps a single instance, wires the global exception handlers,
''' applies the saved theme and starts the background services.
''' </summary>
Public Module Program

    Private _mutex As Mutex

    <STAThread>
    Public Sub Main(args As String())
        ' Headless mode: AVAK.exe --scan <path> [--full]
        If args IsNot Nothing AndAlso args.Any(Function(a) String.Equals(a, "--scan", StringComparison.OrdinalIgnoreCase)) Then
            Environment.ExitCode = RunCliScan(args)
            Return
        End If

        ' Self-test: builds every page and renders it to a PNG. Used to verify the UI
        ' without a human in the loop (AVAK.exe --uitest <folder>).
        If args IsNot Nothing AndAlso args.Any(Function(a) String.Equals(a, "--uitest", StringComparison.OrdinalIgnoreCase)) Then
            Dim i = Array.FindIndex(args, Function(a) String.Equals(a, "--uitest", StringComparison.OrdinalIgnoreCase))
            Environment.ExitCode = RunUiTest(If(i + 1 < args.Length, args(i + 1), AppPaths.Data))
            Return
        End If

        If args IsNot Nothing AndAlso args.Any(Function(a) String.Equals(a, "--selftest", StringComparison.OrdinalIgnoreCase)) Then
            Environment.ExitCode = RunSelfTest()
            Return
        End If

        ' Rule authoring from a script or CI job.
        If HasFlag(args, "--rules") Then
            AppSettings.Load()
            Environment.ExitCode = Content.RuleCli.Run(args)
            Return
        End If

        ' Uninstaller hook: drop the Explorer verb we own.
        If HasFlag(args, "--unregister-shell") Then
            ShellIntegration.Unregister()
            Return
        End If

        ' Build-time helper: regenerate assets\avak.ico from the vector shield.
        If HasFlag(args, "--export-icon") Then
            Try
                AppSettings.Load().ApplyTheme()
                Dim target = ArgValue(args, "--export-icon")
                If String.IsNullOrWhiteSpace(target) Then target = IO.Path.Combine(AppPaths.AppDir, "avak.ico")
                IO.Directory.CreateDirectory(IO.Path.GetDirectoryName(IO.Path.GetFullPath(target)))
                Ui.Icons.SaveIcoFile(target, "shield-check",
                                     Theme.ThemeManager.On_(Theme.ThemeManager.Accent),
                                     Theme.ThemeManager.Accent)
                Environment.ExitCode = 0
            Catch ex As Exception
                Logger.Error("Icon export failed", ex)
                Environment.ExitCode = 1
            End Try
            Return
        End If

        Dim scanUiTarget = ArgValue(args, "--scan-ui")

        Dim createdNew As Boolean
        _mutex = New Mutex(True, "Global\AVAK-SingleInstance", createdNew)
        If Not createdNew Then
            ' Only one AVAK may run. If this instance was started by the Explorer verb,
            ' hand the path to the running copy instead of refusing outright.
            If Not String.IsNullOrWhiteSpace(scanUiTarget) Then
                Try
                    IO.File.WriteAllText(AppPaths.ScanRequestFile, scanUiTarget)
                Catch ex As Exception
                    Logger.Warn("Could not hand off the scan request: " & ex.Message)
                End Try
                Return
            End If
            MessageBox.Show("AVAK is already running - look for the shield in the notification area.",
                            "AVAK", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If

        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2)
        Application.EnableVisualStyles()
        Application.SetCompatibleTextRenderingDefault(False)

        AddHandler Application.ThreadException, AddressOf OnThreadException
        AddHandler AppDomain.CurrentDomain.UnhandledException, AddressOf OnFatal

        Logger.Info("=== AVAK starting ===")

        Dim cfg = AppSettings.Load()
        cfg.ApplyTheme()
        SignatureDatabase.EnsureLoaded()
        QuarantineManager.Reconcile()

        If cfg.RealtimeProtection Then
            If Not RealtimeMonitor.Start() Then
                Logger.Warn("Real-time protection was enabled but could not start.")
            End If
        End If
        ScanScheduler.Start()

        Dim startHidden = cfg.StartMinimized OrElse
                          (args IsNot Nothing AndAlso args.Any(Function(a) String.Equals(a, "--tray", StringComparison.OrdinalIgnoreCase)))

        Dim form As New Forms.MainForm()
        If startHidden Then
            form.WindowState = FormWindowState.Minimized
            form.ShowInTaskbar = False
        End If

        If Not String.IsNullOrWhiteSpace(scanUiTarget) Then
            AddHandler form.Shown, Sub() form.ScanPath(scanUiTarget)
        End If

        Try
            If startHidden Then
                Application.Run(New ApplicationContext())
            Else
                Application.Run(form)
            End If
        Finally
            Shutdown()
        End Try
    End Sub

    ''' <summary>Reads the value that follows a flag, or "" when it is absent.</summary>
    Private Function ArgValue(args As String(), flag As String) As String
        If args Is Nothing Then Return ""
        Dim i = Array.FindIndex(args, Function(a) String.Equals(a, flag, StringComparison.OrdinalIgnoreCase))
        If i < 0 OrElse i + 1 >= args.Length Then Return ""
        Return args(i + 1)
    End Function

    Private Function HasFlag(args As String(), flag As String) As Boolean
        Return args IsNot Nothing AndAlso args.Any(Function(a) String.Equals(a, flag, StringComparison.OrdinalIgnoreCase))
    End Function

    Private Sub Shutdown()
        Try
            RealtimeMonitor.Stop()
            ScanScheduler.Stop()
            SystemInfo.PerfMonitor.Shutdown()
            Logger.Info("=== AVAK stopped ===")
        Catch
        End Try
        Try
            _mutex?.ReleaseMutex()
            _mutex?.Dispose()
        Catch
        End Try
    End Sub

    Private Sub OnThreadException(sender As Object, e As ThreadExceptionEventArgs)
        Logger.Error("Unhandled UI exception", e.Exception)
        MessageBox.Show("Something went wrong:" & Environment.NewLine & Environment.NewLine &
                        e.Exception.Message & Environment.NewLine & Environment.NewLine &
                        "The details were written to:" & Environment.NewLine & AppPaths.Logs,
                        "AVAK", MessageBoxButtons.OK, MessageBoxIcon.Warning)
    End Sub

    Private Sub OnFatal(sender As Object, e As UnhandledExceptionEventArgs)
        Dim ex = TryCast(e.ExceptionObject, Exception)
        Logger.Error("Fatal exception", ex)
    End Sub

    ''' <summary>
    ''' Engine self-test (AVAK.exe --selftest): exercises the signature engine and a
    ''' full quarantine encrypt / restore round trip. Returns the number of failures.
    ''' </summary>
    Private Function RunSelfTest() As Integer
        Dim failures = 0
        Dim probe = IO.Path.Combine(IO.Path.GetTempPath(), "avak-selftest-" & Guid.NewGuid().ToString("N") & ".bin")

        Try
            AppSettings.Load()
            SignatureDatabase.EnsureLoaded()
            Logger.Info("--- self-test start ---")

            ' 1. signature database loaded
            If SignatureDatabase.HashCount = 0 AndAlso SignatureDatabase.PatternCount = 0 Then
                failures += 1
                Logger.Error("selftest: signature database is empty")
            Else
                Logger.Info($"selftest: signatures ok (v{SignatureDatabase.Version}, " &
                            $"{SignatureDatabase.HashCount} hashes, {SignatureDatabase.PatternCount} patterns)")
            End If

            ' 2. pattern engine fires on a known-bad string
            Dim payload = Text.Encoding.ASCII.GetBytes(
                "@echo off" & vbCrLf & "vssadmin delete shadows /all /quiet" & vbCrLf)
            Dim hits = SignatureDatabase.MatchPatterns(payload, payload.Length, ".bat")
            If hits.Count = 0 Then
                failures += 1
                Logger.Error("selftest: pattern matcher did not fire on a known signature")
            Else
                Logger.Info("selftest: pattern matcher ok (" & hits(0).Name & ")")
            End If

            ' 3. quarantine round trip - encrypt, remove, restore, compare
            Dim original(4095) As Byte
            System.Security.Cryptography.RandomNumberGenerator.Fill(original)
            IO.File.WriteAllBytes(probe, original)
            Dim originalHash = SignatureDatabase.Sha256File(probe)

            Dim det As New Detection With {
                .FilePath = probe, .ThreatName = "AVAK-SelfTest", .Severity = Severity.Low,
                .Sha256 = originalHash, .SizeBytes = original.Length}

            Dim q = QuarantineManager.Quarantine(det)
            If Not q.Ok Then
                failures += 1
                Logger.Error("selftest: quarantine failed - " & q.Message)
            ElseIf IO.File.Exists(probe) Then
                failures += 1
                Logger.Error("selftest: the original file was not removed")
            Else
                Dim item = QuarantineManager.All().FirstOrDefault(Function(i) i.ThreatName = "AVAK-SelfTest")
                If item Is Nothing Then
                    failures += 1
                    Logger.Error("selftest: quarantined item missing from the index")
                Else
                    Dim r = QuarantineManager.Restore(item.Id)
                    If Not r.Ok Then
                        failures += 1
                        Logger.Error("selftest: restore failed - " & r.Message)
                    ElseIf SignatureDatabase.Sha256File(probe) <> originalHash Then
                        failures += 1
                        Logger.Error("selftest: restored bytes do not match the original")
                    Else
                        Logger.Info("selftest: quarantine encrypt/restore round trip ok")
                    End If
                End If
            End If

            ' 4. settings + profile persistence
            Dim before = AppSettings.Current.TotalScans
            AppSettings.Current.TotalScans = before + 1
            AppSettings.Current.Save()
            If AppSettings.Load().TotalScans <> before + 1 Then
                failures += 1
                Logger.Error("selftest: settings did not persist")
            Else
                AppSettings.Current.TotalScans = before
                AppSettings.Current.Save()
                Logger.Info("selftest: settings persistence ok")
            End If

            Logger.Info($"--- self-test finished, {failures} failure(s) ---")
        Catch ex As Exception
            failures += 1
            Logger.Error("selftest crashed", ex)
        Finally
            Try
                If IO.File.Exists(probe) Then IO.File.Delete(probe)
            Catch
            End Try
            For Each leftover In QuarantineManager.All().Where(Function(i) i.ThreatName = "AVAK-SelfTest")
                QuarantineManager.DeleteForever(leftover.Id)
            Next
        End Try

        Return failures
    End Function

    Private Function RunUiTest(outDir As String) As Integer
        Dim failures = 0
        Try
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2)
            Application.EnableVisualStyles()
            Application.SetCompatibleTextRenderingDefault(False)
            AppSettings.Load().ApplyTheme()
            SignatureDatabase.EnsureLoaded()
            IO.Directory.CreateDirectory(outDir)

            Dim form As New Forms.MainForm()
            form.Show()
            Pump(400)

            For Each key In form.ViewKeys
                Try
                    form.Navigate(key)
                    Pump(900)
                    Using bmp As New Drawing.Bitmap(form.Width, form.Height)
                        form.DrawToBitmap(bmp, New Drawing.Rectangle(0, 0, form.Width, form.Height))
                        bmp.Save(IO.Path.Combine(outDir, key & ".png"), Drawing.Imaging.ImageFormat.Png)
                    End Using
                    Logger.Info("uitest ok: " & key)
                Catch ex As Exception
                    failures += 1
                    Logger.Error("uitest FAILED: " & key, ex)
                End Try
            Next

            form.Close()
            form.Dispose()
        Catch ex As Exception
            failures += 1
            Logger.Error("uitest harness failed", ex)
        End Try
        Return failures
    End Function

    Private Sub Pump(ms As Integer)
        Dim until = DateTime.UtcNow.AddMilliseconds(ms)
        While DateTime.UtcNow < until
            Application.DoEvents()
            Thread.Sleep(20)
        End While
    End Sub

    ''' <summary>
    ''' Console scan: AVAK.exe --scan "C:\folder" [--full]
    ''' Writes a JSON report next to the other AVAK data and returns the threat
    ''' count as the exit code, so it can be wired into scripts or CI.
    ''' </summary>
    Private Function RunCliScan(args As String()) As Integer
        Try
            Dim idx = Array.FindIndex(args, Function(a) String.Equals(a, "--scan", StringComparison.OrdinalIgnoreCase))
            Dim target = If(idx >= 0 AndAlso idx + 1 < args.Length, args(idx + 1), "")
            Dim full = args.Any(Function(a) String.Equals(a, "--full", StringComparison.OrdinalIgnoreCase))

            AppSettings.Load()
            SignatureDatabase.EnsureLoaded()

            Dim req As New ScanRequest With {
                .Profile = If(String.IsNullOrWhiteSpace(target), If(full, ScanProfile.Full, ScanProfile.Quick), ScanProfile.Custom),
                .Recursive = True
            }
            If Not String.IsNullOrWhiteSpace(target) Then
                req.Roots.Add(target)
            Else
                req.Roots.AddRange(ScanEngine.DefaultRoots(req.Profile))
            End If

            Dim engine As New ScanEngine()
            Dim report = engine.ScanAsync(req, Nothing, Threading.CancellationToken.None).
                                GetAwaiter().GetResult()

            Dim outPath = IO.Path.Combine(AppPaths.Data, "cli-report.json")
            Json.Save(outPath, report)
            HistoryStore.Add(report)

            Logger.Info($"CLI scan: {report.FilesScanned} files, {report.ThreatCount} threats -> {outPath}")
            Return report.ThreatCount
        Catch ex As Exception
            Logger.Error("CLI scan failed", ex)
            Return -1
        End Try
    End Function

End Module
