Imports System.Diagnostics
Imports System.Runtime.InteropServices
Imports AVAK.Core

Namespace SystemInfo

    Public Class PerfSample
        Public Property CpuPercent As Single
        Public Property RamPercent As Single
        Public Property RamUsedBytes As Long
        Public Property RamTotalBytes As Long
        Public Property DiskPercent As Single
        Public Property NetKbPerSec As Single
        Public Property ProcessCount As Integer
        Public Property ThreadCount As Integer
        Public Property Uptime As TimeSpan
        Public Property At As DateTime = DateTime.Now
    End Class

    ''' <summary>
    ''' Live counters. Performance counters can be slow or unavailable on locked-down
    ''' machines, so every read is guarded and falls back to GlobalMemoryStatusEx /
    ''' process enumeration rather than throwing.
    ''' </summary>
    Public NotInheritable Class PerfMonitor

        Private Sub New()
        End Sub

        Private Shared _cpu As PerformanceCounter
        Private Shared _disk As PerformanceCounter
        Private Shared _net As PerformanceCounter()
        Private Shared _initTried As Boolean
        Private Shared _lastCpu As Single

        <StructLayout(LayoutKind.Sequential, CharSet:=CharSet.Auto)>
        Private Structure MEMORYSTATUSEX
            Public dwLength As UInteger
            Public dwMemoryLoad As UInteger
            Public ullTotalPhys As ULong
            Public ullAvailPhys As ULong
            Public ullTotalPageFile As ULong
            Public ullAvailPageFile As ULong
            Public ullTotalVirtual As ULong
            Public ullAvailVirtual As ULong
            Public ullAvailExtendedVirtual As ULong
        End Structure

        <DllImport("kernel32.dll", CharSet:=CharSet.Auto, SetLastError:=True)>
        Private Shared Function GlobalMemoryStatusEx(ByRef lpBuffer As MEMORYSTATUSEX) As Boolean
        End Function

        Private Shared Sub Init()
            If _initTried Then Return
            _initTried = True
            Try
                _cpu = New PerformanceCounter("Processor Information", "% Processor Utility", "_Total", True)
                _cpu.NextValue()
            Catch
                Try
                    _cpu = New PerformanceCounter("Processor", "% Processor Time", "_Total", True)
                    _cpu.NextValue()
                Catch ex As Exception
                    _cpu = Nothing
                    Logger.Warn("CPU performance counter unavailable: " & ex.Message)
                End Try
            End Try

            Try
                _disk = New PerformanceCounter("PhysicalDisk", "% Disk Time", "_Total", True)
                _disk.NextValue()
            Catch
                _disk = Nothing
            End Try

            Try
                Dim cat As New PerformanceCounterCategory("Network Interface")
                Dim names = cat.GetInstanceNames().
                    Where(Function(n) Not n.Contains("Loopback") AndAlso Not n.Contains("isatap") AndAlso
                                      Not n.Contains("Pseudo")).ToArray()
                _net = names.Select(Function(n) New PerformanceCounter("Network Interface", "Bytes Total/sec", n, True)).ToArray()
                For Each c In _net
                    c.NextValue()
                Next
            Catch
                _net = Nothing
            End Try
        End Sub

        Public Shared Function Read() As PerfSample
            Init()
            Dim s As New PerfSample()

            ' CPU
            Try
                If _cpu IsNot Nothing Then
                    s.CpuPercent = Math.Max(0.0F, Math.Min(100.0F, _cpu.NextValue()))
                    _lastCpu = s.CpuPercent
                Else
                    s.CpuPercent = _lastCpu
                End If
            Catch
                s.CpuPercent = _lastCpu
            End Try

            ' RAM - always available via kernel32
            Try
                Dim m As New MEMORYSTATUSEX()
                m.dwLength = CUInt(Marshal.SizeOf(GetType(MEMORYSTATUSEX)))
                If GlobalMemoryStatusEx(m) Then
                    s.RamTotalBytes = CLng(m.ullTotalPhys)
                    s.RamUsedBytes = CLng(m.ullTotalPhys - m.ullAvailPhys)
                    s.RamPercent = CSng(m.dwMemoryLoad)
                End If
            Catch
            End Try

            Try
                If _disk IsNot Nothing Then s.DiskPercent = Math.Max(0.0F, Math.Min(100.0F, _disk.NextValue()))
            Catch
            End Try

            Try
                If _net IsNot Nothing AndAlso _net.Length > 0 Then
                    Dim total As Single = 0
                    For Each c In _net
                        total += c.NextValue()
                    Next
                    s.NetKbPerSec = total / 1024.0F
                End If
            Catch
            End Try

            Try
                Dim procs = Process.GetProcesses()
                s.ProcessCount = procs.Length
                Dim threads = 0
                For Each p In procs
                    Try
                        threads += p.Threads.Count
                    Catch
                    Finally
                        p.Dispose()
                    End Try
                Next
                s.ThreadCount = threads
            Catch
            End Try

            Try
                s.Uptime = TimeSpan.FromMilliseconds(Environment.TickCount64)
            Catch
            End Try

            Return s
        End Function

        Public Shared Sub Shutdown()
            Try
                _cpu?.Dispose()
                _disk?.Dispose()
                If _net IsNot Nothing Then
                    For Each c In _net
                        c.Dispose()
                    Next
                End If
            Catch
            End Try
        End Sub

    End Class

End Namespace
