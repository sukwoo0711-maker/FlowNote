using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Principal;

namespace FlowNote.Infrastructure.Assist.Embedded;

internal static class KeyFileAcl
{
    [SupportedOSPlatform("windows")]
    public static void RestrictToCurrentUser(string path)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
        var sid = WindowsIdentity.GetCurrent().User?.Value;
        if (string.IsNullOrWhiteSpace(sid))
        {
            return;
        }

        var sddl = $"D:P(A;;FA;;;SY)(A;;FA;;;{sid})";
        if (!ConvertStringSecurityDescriptorToSecurityDescriptor(sddl, 1, out var descriptor, IntPtr.Zero))
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError(), "key-acl-sddl-failed");
        }

        try
        {
            if (!SetFileSecurity(path, 4, descriptor))
            {
                throw new Win32Exception(Marshal.GetLastPInvokeError(), "key-acl-set-failed");
            }
        }
        finally
        {
            LocalFree(descriptor);
        }
    }

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool ConvertStringSecurityDescriptorToSecurityDescriptor(
        string sddl,
        uint revision,
        out IntPtr descriptor,
        IntPtr size);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool SetFileSecurity(string path, uint info, IntPtr descriptor);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr LocalFree(IntPtr handle);
}
