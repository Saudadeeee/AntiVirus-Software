Imports System.IO
Imports System.Management
Imports Microsoft.Win32
Imports AVAK.Core

Namespace SystemInfo

    Public Class DiskInfo
        Public Property Name As String = ""
        Public Property Label As String = ""
        Public Property Format As String = ""
        Public Property TotalBytes As Long
        Public Property FreeBytes As Long
        Public ReadOnly Property UsedBytes As Long
            Get
                Return Math.Max(0, TotalBytes - FreeBytes)
            End Get
        End Property
        Public ReadOnly Property UsedPercent As Single
            Get
                Return If(TotalBytes <= 0, 0.0F, CSng(UsedBytes * 100.0R / TotalBytes))
            End Get
        End Property
    End Class

    Public Class MachineInfo
        Public Property ComputerName As String = ""
        Public Property UserName As String = ""
        Public Property OsName As String = ""
        Public Property OsVersion As String = ""
        Public Property OsArchitecture As String = ""
        Public Property InstallDate As DateTime = DateTime.MinValue
        Public Property Manufacturer As String = ""
        Public Property Model As String = ""
        Public Property BiosVersion As String = ""

        Public Property CpuName As String = ""
        Public Property CpuCores As Integer
        Public Property CpuThreads As Integer
        Public Property CpuMhz As Integer

        Public Property RamTotalBytes As Long
        Public Property RamSlots As String = ""
        Public Property GpuNames As New List(Of String)()
        Public Property Disks As New List(Of DiskInfo)()
        Public Property BatteryPercent As Integer = -1
        Public Property BatteryCharging As Boolean = False
    End Class

    ''' <summary>Reads real hardware and OS facts via WMI + registry, with safe fallbacks.</summary>
    Public NotInheritable Class HardwareInfo

        Private Sub New()
        End Sub

        Private Shared _cached As MachineInfo
        Private Shared _cachedAt As DateTime = DateTime.MinValue

        Public Shared Function Get_(Optional forceRefresh As Boolean = False) As MachineInfo
            If Not forceRefresh AndAlso _cached IsNot Nothing AndAlso
               (DateTime.Now - _cachedAt).TotalMinutes < 5 Then
                RefreshVolatile(_cached)
                Return _cached
            End If

            Dim m As New MachineInfo()

            Try
                m.ComputerName = Environment.MachineName
                m.UserName = Environment.UserName
                m.OsVersion = Environment.OSVersion.VersionString
                m.OsArchitecture = If(Environment.Is64BitOperatingSystem, "64-bit", "32-bit")
            Catch
            End Try

            ' CPU from registry (fast, no WMI round trip)
            Try
                Using k = Registry.LocalMachine.OpenSubKey("HARDWARE\DESCRIPTION\System\CentralProcessor\0")
                    If k IsNot Nothing Then
                        m.CpuName = Convert.ToString(k.GetValue("ProcessorNameString", "")).Trim()
                        m.CpuMhz = CInt(Convert.ToInt64(k.GetValue("~MHz", 0L)))
                    End If
                End Using
                m.CpuThreads = Environment.ProcessorCount
            Catch ex As Exception
                Logger.Warn("CPU registry read failed: " & ex.Message)
            End Try

            Try
                Using k = Registry.LocalMachine.OpenSubKey("SOFTWARE\Microsoft\Windows NT\CurrentVersion")
                    If k IsNot Nothing Then
                        Dim product = Convert.ToString(k.GetValue("ProductName", ""))
                        Dim display = Convert.ToString(k.GetValue("DisplayVersion", ""))
                        Dim build = Convert.ToString(k.GetValue("CurrentBuildNumber", ""))
                        Dim ubr = Convert.ToString(k.GetValue("UBR", ""))
                        ' Windows 11 still reports "Windows 10 ..." in ProductName
                        Dim buildNum = 0
                        Integer.TryParse(build, buildNum)
                        If buildNum >= 22000 AndAlso product.Contains("Windows 10") Then
                            product = product.Replace("Windows 10", "Windows 11")
                        End If
                        m.OsName = product
                        m.OsVersion = (If(String.IsNullOrEmpty(display), "", display & " ")) &
                                      "(build " & build & If(String.IsNullOrEmpty(ubr), "", "." & ubr) & ")"
                    End If
                End Using
            Catch
            End Try

            ' WMI details
            Wmi("SELECT Manufacturer, Model, TotalPhysicalMemory FROM Win32_ComputerSystem",
                Sub(o)
                    m.Manufacturer = Str(o, "Manufacturer")
                    m.Model = Str(o, "Model")
                    Dim ram = Str(o, "TotalPhysicalMemory")
                    Dim v As Long
                    If Long.TryParse(ram, v) Then m.RamTotalBytes = v
                End Sub)

            Wmi("SELECT NumberOfCores, NumberOfLogicalProcessors, MaxClockSpeed, Name FROM Win32_Processor",
                Sub(o)
                    If m.CpuCores = 0 Then m.CpuCores = Num(o, "NumberOfCores")
                    If m.CpuThreads = 0 Then m.CpuThreads = Num(o, "NumberOfLogicalProcessors")
                    If m.CpuMhz = 0 Then m.CpuMhz = Num(o, "MaxClockSpeed")
                    If String.IsNullOrEmpty(m.CpuName) Then m.CpuName = Str(o, "Name")
                End Sub)

            Wmi("SELECT SMBIOSBIOSVersion FROM Win32_BIOS",
                Sub(o) m.BiosVersion = Str(o, "SMBIOSBIOSVersion"))

            Wmi("SELECT Caption, InstallDate FROM Win32_OperatingSystem",
                Sub(o)
                    If String.IsNullOrEmpty(m.OsName) Then m.OsName = Str(o, "Caption")
                    Dim d = Str(o, "InstallDate")
                    If d.Length >= 8 Then
                        Dim y, mo, dd As Integer
                        If Integer.TryParse(d.Substring(0, 4), y) AndAlso
                           Integer.TryParse(d.Substring(4, 2), mo) AndAlso
                           Integer.TryParse(d.Substring(6, 2), dd) Then
                            Try
                                m.InstallDate = New DateTime(y, mo, dd)
                            Catch
                            End Try
                        End If
                    End If
                End Sub)

            Wmi("SELECT Name FROM Win32_VideoController",
                Sub(o)
                    Dim n = Str(o, "Name")
                    If Not String.IsNullOrWhiteSpace(n) Then m.GpuNames.Add(n)
                End Sub)

            Dim sticks As New List(Of String)()
            Wmi("SELECT Capacity, Speed FROM Win32_PhysicalMemory",
                Sub(o)
                    Dim cap = Str(o, "Capacity")
                    Dim spd = Num(o, "Speed")
                    Dim v As Long
                    If Long.TryParse(cap, v) Then sticks.Add(Fmt.Bytes(v) & If(spd > 0, " @ " & spd & " MHz", ""))
                End Sub)
            m.RamSlots = String.Join(" + ", sticks)

            RefreshVolatile(m)

            _cached = m
            _cachedAt = DateTime.Now
            Return m
        End Function

        ''' <summary>Refreshes the parts that change minute to minute.</summary>
        Private Shared Sub RefreshVolatile(m As MachineInfo)
            m.Disks.Clear()
            Try
                For Each d In DriveInfo.GetDrives()
                    Try
                        If Not d.IsReady Then Continue For
                        If d.DriveType <> DriveType.Fixed AndAlso d.DriveType <> DriveType.Removable Then Continue For
                        m.Disks.Add(New DiskInfo With {
                            .Name = d.Name,
                            .Label = If(String.IsNullOrWhiteSpace(d.VolumeLabel), "Local disk", d.VolumeLabel),
                            .Format = d.DriveFormat,
                            .TotalBytes = d.TotalSize,
                            .FreeBytes = d.AvailableFreeSpace})
                    Catch
                    End Try
                Next
            Catch
            End Try

            Try
                Dim ps = Global.System.Windows.Forms.SystemInformation.PowerStatus
                If ps.BatteryChargeStatus <> Global.System.Windows.Forms.BatteryChargeStatus.NoSystemBattery AndAlso
                   ps.BatteryLifePercent <= 1.0F Then
                    m.BatteryPercent = CInt(ps.BatteryLifePercent * 100)
                    m.BatteryCharging = ps.PowerLineStatus = Global.System.Windows.Forms.PowerLineStatus.Online
                Else
                    m.BatteryPercent = -1
                End If
            Catch
                m.BatteryPercent = -1
            End Try

            If m.RamTotalBytes <= 0 Then
                Try
                    m.RamTotalBytes = PerfMonitor.Read().RamTotalBytes
                Catch
                End Try
            End If
        End Sub

        ' -- WMI helpers ------------------------------------------------------

        Private Shared Sub Wmi(query As String, each_ As Action(Of ManagementBaseObject))
            Try
                Using searcher As New ManagementObjectSearcher("root\CIMV2", query)
                    Using results = searcher.Get()
                        For Each o As ManagementBaseObject In results
                            Try
                                each_(o)
                            Finally
                                o.Dispose()
                            End Try
                        Next
                    End Using
                End Using
            Catch ex As Exception
                Logger.Warn("WMI query failed (" & query & "): " & ex.Message)
            End Try
        End Sub

        Private Shared Function Str(o As ManagementBaseObject, prop As String) As String
            Try
                Return Convert.ToString(o(prop))?.Trim()
            Catch
                Return ""
            End Try
        End Function

        Private Shared Function Num(o As ManagementBaseObject, prop As String) As Integer
            Try
                Return Convert.ToInt32(o(prop))
            Catch
                Return 0
            End Try
        End Function

    End Class

End Namespace
