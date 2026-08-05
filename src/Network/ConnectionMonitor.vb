Imports System.Net
Imports System.Net.NetworkInformation
Imports System.Runtime.InteropServices
Imports AVAK.Core

Namespace Network

    Public Class ConnectionRow
        Public Property LocalAddress As String = ""
        Public Property LocalPort As Integer
        Public Property RemoteAddress As String = ""
        Public Property RemotePort As Integer
        Public Property State As String = ""
        Public Property Pid As Integer
        Public Property ProcessName As String = ""
        Public Property ProcessPath As String = ""
        Public Property IsListening As Boolean
        Public Property IsLoopback As Boolean
        Public Property IsExternal As Boolean

        Public ReadOnly Property Local As String
            Get
                Return LocalAddress & ":" & LocalPort
            End Get
        End Property

        Public ReadOnly Property Remote As String
            Get
                Return If(IsListening, "-", RemoteAddress & ":" & RemotePort)
            End Get
        End Property
    End Class

    Public Class AdapterRow
        Public Property Name As String = ""
        Public Property Description As String = ""
        Public Property Kind As String = ""
        Public Property Status As String = ""
        Public Property SpeedMbps As Long
        Public Property Mac As String = ""
        Public Property Addresses As New List(Of String)()
        Public Property Gateways As New List(Of String)()
        Public Property DnsServers As New List(Of String)()
        Public Property BytesSent As Long
        Public Property BytesReceived As Long
    End Class

    ''' <summary>
    ''' Live TCP table with owning process (GetExtendedTcpTable), plus adapter facts.
    ''' Everything here is read-only observation - no traffic is intercepted.
    ''' </summary>
    Public NotInheritable Class ConnectionMonitor

        Private Sub New()
        End Sub

        <StructLayout(LayoutKind.Sequential)>
        Private Structure MIB_TCPROW_OWNER_PID
            Public state As UInteger
            Public localAddr As UInteger
            Public localPort1 As Byte
            Public localPort2 As Byte
            Public localPort3 As Byte
            Public localPort4 As Byte
            Public remoteAddr As UInteger
            Public remotePort1 As Byte
            Public remotePort2 As Byte
            Public remotePort3 As Byte
            Public remotePort4 As Byte
            Public owningPid As UInteger
        End Structure

        <DllImport("iphlpapi.dll", SetLastError:=True)>
        Private Shared Function GetExtendedTcpTable(pTcpTable As IntPtr, ByRef dwOutBufLen As Integer,
                                                    sort As Boolean, ipVersion As Integer,
                                                    tblClass As Integer, reserved As Integer) As Integer
        End Function

        Private Const AF_INET As Integer = 2
        Private Const TCP_TABLE_OWNER_PID_ALL As Integer = 5

        Private Shared ReadOnly StateNames As String() = {
            "", "Closed", "Listening", "SynSent", "SynReceived", "Established", "FinWait1",
            "FinWait2", "CloseWait", "Closing", "LastAck", "TimeWait", "DeleteTcb"}

        Public Shared Function Connections(Optional resolveProcesses As Boolean = True) As List(Of ConnectionRow)
            Dim rows As New List(Of ConnectionRow)()
            Dim buf As IntPtr = IntPtr.Zero
            Try
                Dim size As Integer = 0
                GetExtendedTcpTable(IntPtr.Zero, size, True, AF_INET, TCP_TABLE_OWNER_PID_ALL, 0)
                If size <= 0 Then Return rows

                buf = Marshal.AllocHGlobal(size)
                If GetExtendedTcpTable(buf, size, True, AF_INET, TCP_TABLE_OWNER_PID_ALL, 0) <> 0 Then Return rows

                Dim count = Marshal.ReadInt32(buf)
                Dim rowSize = Marshal.SizeOf(GetType(MIB_TCPROW_OWNER_PID))
                Dim ptr = IntPtr.Add(buf, 4)

                Dim names As Dictionary(Of Integer, (Name As String, Path As String)) = Nothing
                If resolveProcesses Then names = ProcessNameMap()

                For i = 0 To count - 1
                    Dim r = Marshal.PtrToStructure(Of MIB_TCPROW_OWNER_PID)(ptr)
                    ptr = IntPtr.Add(ptr, rowSize)

                    Dim localIp = New IPAddress(BitConverter.GetBytes(r.localAddr)).ToString()
                    Dim remoteIp = New IPAddress(BitConverter.GetBytes(r.remoteAddr)).ToString()
                    Dim lport = (CInt(r.localPort1) << 8) Or r.localPort2
                    Dim rport = (CInt(r.remotePort1) << 8) Or r.remotePort2
                    Dim stateIdx = CInt(r.state)
                    Dim stateName = If(stateIdx >= 0 AndAlso stateIdx < StateNames.Length, StateNames(stateIdx), "?")

                    Dim row As New ConnectionRow With {
                        .LocalAddress = localIp,
                        .LocalPort = lport,
                        .RemoteAddress = remoteIp,
                        .RemotePort = rport,
                        .State = stateName,
                        .Pid = CInt(r.owningPid),
                        .IsListening = (stateIdx = 2)
                    }
                    row.IsLoopback = localIp.StartsWith("127.") OrElse remoteIp.StartsWith("127.")
                    row.IsExternal = Not row.IsListening AndAlso Not row.IsLoopback AndAlso
                                     Not IsPrivate(remoteIp) AndAlso remoteIp <> "0.0.0.0"

                    If names IsNot Nothing Then
                        Dim hit As (Name As String, Path As String) = ("", "")
                        If names.TryGetValue(row.Pid, hit) Then
                            row.ProcessName = hit.Name
                            row.ProcessPath = hit.Path
                        End If
                    End If

                    rows.Add(row)
                Next
            Catch ex As Exception
                Logger.Warn("TCP table read failed: " & ex.Message)
            Finally
                If buf <> IntPtr.Zero Then Marshal.FreeHGlobal(buf)
            End Try

            Return rows.OrderByDescending(Function(r) r.IsExternal).
                        ThenBy(Function(r) r.ProcessName).
                        ThenBy(Function(r) r.LocalPort).ToList()
        End Function

        Private Shared Function ProcessNameMap() As Dictionary(Of Integer, (Name As String, Path As String))
            Dim d As New Dictionary(Of Integer, (Name As String, Path As String))()
            For Each p In Process.GetProcesses()
                Try
                    Dim path = ""
                    Try
                        path = If(p.MainModule?.FileName, "")
                    Catch
                    End Try
                    d(p.Id) = (p.ProcessName, path)
                Catch
                Finally
                    p.Dispose()
                End Try
            Next
            Return d
        End Function

        Public Shared Function IsPrivate(ip As String) As Boolean
            If String.IsNullOrEmpty(ip) Then Return True
            Dim parts = ip.Split("."c)
            If parts.Length <> 4 Then Return True
            Dim a, b As Integer
            If Not Integer.TryParse(parts(0), a) OrElse Not Integer.TryParse(parts(1), b) Then Return True
            If a = 10 Then Return True
            If a = 127 Then Return True
            If a = 172 AndAlso b >= 16 AndAlso b <= 31 Then Return True
            If a = 192 AndAlso b = 168 Then Return True
            If a = 169 AndAlso b = 254 Then Return True
            If a = 0 Then Return True
            Return False
        End Function

        Public Shared Function Adapters() As List(Of AdapterRow)
            Dim list As New List(Of AdapterRow)()
            Try
                For Each ni In NetworkInterface.GetAllNetworkInterfaces()
                    Try
                        If ni.NetworkInterfaceType = NetworkInterfaceType.Loopback Then Continue For
                        Dim row As New AdapterRow With {
                            .Name = ni.Name,
                            .Description = ni.Description,
                            .Kind = ni.NetworkInterfaceType.ToString(),
                            .Status = ni.OperationalStatus.ToString(),
                            .Mac = String.Join(":", ni.GetPhysicalAddress().GetAddressBytes().Select(Function(x) x.ToString("X2")))
                        }
                        Try
                            row.SpeedMbps = ni.Speed \ 1000000L
                        Catch
                        End Try

                        Dim props = ni.GetIPProperties()
                        For Each ua In props.UnicastAddresses
                            If ua.Address.AddressFamily = Sockets.AddressFamily.InterNetwork Then
                                row.Addresses.Add(ua.Address.ToString())
                            End If
                        Next
                        For Each gw In props.GatewayAddresses
                            row.Gateways.Add(gw.Address.ToString())
                        Next
                        For Each dns In props.DnsAddresses
                            If dns.AddressFamily = Sockets.AddressFamily.InterNetwork Then row.DnsServers.Add(dns.ToString())
                        Next

                        Try
                            Dim s = ni.GetIPv4Statistics()
                            row.BytesSent = s.BytesSent
                            row.BytesReceived = s.BytesReceived
                        Catch
                        End Try

                        list.Add(row)
                    Catch
                    End Try
                Next
            Catch ex As Exception
                Logger.Warn("Adapter enumeration failed: " & ex.Message)
            End Try
            Return list.OrderByDescending(Function(a) a.Status = "Up").ThenBy(Function(a) a.Name).ToList()
        End Function

        Public Shared Function PublicIpHint() As String
            Try
                For Each a In Adapters()
                    If a.Status = "Up" AndAlso a.Addresses.Count > 0 Then Return a.Addresses(0)
                Next
            Catch
            End Try
            Return "-"
        End Function

    End Class

End Namespace
