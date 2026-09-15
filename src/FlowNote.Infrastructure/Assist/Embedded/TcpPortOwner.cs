using System.Net;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace FlowNote.Infrastructure.Assist.Embedded;

public static class TcpPortOwner
{
    private const int AfInet = 2;
    private const int TcpTableOwnerPidListener = 3;
    private const uint LoopbackAddr = 0x0100007F;
    private const uint ListenState = 2;

    [SupportedOSPlatform("windows")]
    public static bool TryGetListenerPid(int port, out int processId)
    {
        processId = 0;
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        var size = 0;
        GetExtendedTcpTable(IntPtr.Zero, ref size, true, AfInet, TcpTableOwnerPidListener, 0);
        var buffer = Marshal.AllocHGlobal(size);
        try
        {
            if (GetExtendedTcpTable(buffer, ref size, true, AfInet, TcpTableOwnerPidListener, 0) != 0)
            {
                return false;
            }

            var count = Marshal.ReadInt32(buffer);
            var rowPtr = buffer + 4;
            var rowSize = Marshal.SizeOf<MibTcpRowOwnerPid>();
            for (var i = 0; i < count; i++)
            {
                var row = Marshal.PtrToStructure<MibTcpRowOwnerPid>(rowPtr + i * rowSize);
                if (row.State != ListenState)
                {
                    continue;
                }

                var localPort = IPAddress.NetworkToHostOrder(unchecked((short)(row.LocalPort & 0xFFFF))) & 0xFFFF;
                if (localPort == port && row.LocalAddr == LoopbackAddr)
                {
                    processId = (int)row.OwningPid;
                    return true;
                }
            }

            return false;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MibTcpRowOwnerPid
    {
        public uint State;
        public uint LocalAddr;
        public uint LocalPort;
        public uint RemoteAddr;
        public uint RemotePort;
        public uint OwningPid;
    }

    [DllImport("iphlpapi.dll", SetLastError = true)]
    private static extern uint GetExtendedTcpTable(
        IntPtr table,
        ref int size,
        bool order,
        int addressFamily,
        int tableClass,
        int reserved);
}
