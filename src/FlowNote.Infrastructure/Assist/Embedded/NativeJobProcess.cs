using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using FlowNote.Core.Assist;
using Microsoft.Win32.SafeHandles;

namespace FlowNote.Infrastructure.Assist.Embedded;

public sealed class NativeJobProcess : IDisposable
{
    private const uint JobExtendedLimitInfoClass = 9;
    private const uint JobObjectLimitKillOnJobClose = 0x2000;
    private const uint CreateUnicodeEnvironment = 0x00000400;
    private const uint CreateNoWindow = 0x08000000;
    private const uint ExtendedStartupInfoPresent = 0x00080000;
    private const uint StartfUseStdHandles = 0x00000100;
    private const uint StartfUseShowWindow = 0x00000001;
    private const nint ProcThreadAttributeJobList = 0x0002000D;
    private const nint ProcThreadAttributeHandleList = 0x00020002;
    private const int SwHide = 0;

    private readonly IntPtr _job;
    private readonly ProcessWaitHandle _wait;
    private readonly FileStream _stdout;
    private readonly FileStream _stderr;
    private readonly CancellationTokenSource _drain = new();
    private readonly StringBuilder _diagnostics = new();
    private bool _disposed;

    private NativeJobProcess(IntPtr job, IntPtr process, int processId, FileStream stdout, FileStream stderr)
    {
        _job = job;
        ProcessHandle = process;
        ProcessId = processId;
        _wait = new ProcessWaitHandle(process);
        _stdout = stdout;
        _stderr = stderr;
        _ = DrainAsync(_stdout, _drain.Token);
        _ = DrainAsync(_stderr, _drain.Token);
    }

    public IntPtr ProcessHandle { get; }

    public int ProcessId { get; }

    public bool HasExited => _wait.WaitOne(0);

    [SupportedOSPlatform("windows")]
    public static NativeJobProcess Start(
        string executable,
        IReadOnlyList<string> arguments,
        IReadOnlyDictionary<string, string> environment)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("job-object-windows-only");
        }

        var exe = Path.GetFullPath(executable);
        if (!File.Exists(exe))
        {
            throw new FileNotFoundException("engine-exe-missing", exe);
        }

        var name = Path.GetFileName(exe);
        if (string.Equals(name, "ping.exe", StringComparison.OrdinalIgnoreCase))
        {
            var systemPing = Path.GetFullPath(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.System),
                "ping.exe"));
            if (!string.Equals(exe, systemPing, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("engine-exe-not-allowed");
            }
        }
        else if (!string.Equals(name, "llama-server.exe", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("engine-exe-not-allowed");
        }

        var job = CreateJobObjectW(IntPtr.Zero, null);
        if (job == IntPtr.Zero)
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError(), "create-job-failed");
        }

        try
        {
            ApplyKillOnClose(job);
            var (stdoutRead, stdoutWrite) = CreatePipePair();
            var (stderrRead, stderrWrite) = CreatePipePair();
            var attributeSize = IntPtr.Zero;
            InitializeProcThreadAttributeList(IntPtr.Zero, 2, 0, ref attributeSize);
            var attributeList = Marshal.AllocHGlobal(attributeSize);
            var jobList = Marshal.AllocHGlobal(IntPtr.Size);
            var handleList = Marshal.AllocHGlobal(IntPtr.Size * 2);
            var environmentBlock = BuildEnvironment(environment);
            var commandLine = Win32CommandLine.Build(exe, arguments);
            var commandBuffer = new StringBuilder(commandLine);
            try
            {
                if (!InitializeProcThreadAttributeList(attributeList, 2, 0, ref attributeSize))
                {
                    throw new Win32Exception(Marshal.GetLastPInvokeError(), "init-attributes-failed");
                }

                Marshal.WriteIntPtr(jobList, job);
                if (!UpdateProcThreadAttribute(
                        attributeList, 0, ProcThreadAttributeJobList, jobList, (nuint)IntPtr.Size, IntPtr.Zero, IntPtr.Zero))
                {
                    throw new Win32Exception(Marshal.GetLastPInvokeError(), "job-list-attribute-failed");
                }

                Marshal.WriteIntPtr(handleList, stdoutWrite.DangerousGetHandle());
                Marshal.WriteIntPtr(handleList, IntPtr.Size, stderrWrite.DangerousGetHandle());
                if (!UpdateProcThreadAttribute(
                        attributeList,
                        0,
                        ProcThreadAttributeHandleList,
                        handleList,
                        (nuint)(IntPtr.Size * 2),
                        IntPtr.Zero,
                        IntPtr.Zero))
                {
                    throw new Win32Exception(Marshal.GetLastPInvokeError(), "handle-list-attribute-failed");
                }

                var startup = new StartupInfoEx
                {
                    StartupInfo = new StartupInfo
                    {
                        cb = Marshal.SizeOf<StartupInfoEx>(),
                        dwFlags = StartfUseStdHandles | StartfUseShowWindow,
                        wShowWindow = SwHide,
                        hStdOutput = stdoutWrite.DangerousGetHandle(),
                        hStdError = stderrWrite.DangerousGetHandle()
                    },
                    lpAttributeList = attributeList
                };

                var created = CreateProcessW(
                    exe,
                    commandBuffer,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    true,
                    CreateUnicodeEnvironment | CreateNoWindow | ExtendedStartupInfoPresent,
                    environmentBlock,
                    Path.GetDirectoryName(exe),
                    ref startup,
                    out var processInfo);
                if (!created)
                {
                    throw new Win32Exception(Marshal.GetLastPInvokeError(), "create-process-failed");
                }

                CloseHandle(processInfo.hThread);
                stdoutWrite.Dispose();
                stderrWrite.Dispose();
                return new NativeJobProcess(
                    job,
                    processInfo.hProcess,
                    processInfo.dwProcessId,
                    new FileStream(stdoutRead, FileAccess.Read),
                    new FileStream(stderrRead, FileAccess.Read));
            }
            finally
            {
                DeleteProcThreadAttributeList(attributeList);
                Marshal.FreeHGlobal(attributeList);
                Marshal.FreeHGlobal(jobList);
                Marshal.FreeHGlobal(handleList);
                Marshal.FreeHGlobal(environmentBlock);
            }
        }
        catch
        {
            CloseHandle(job);
            throw;
        }
    }

    public bool WaitForExit(TimeSpan timeout) => _wait.WaitOne(timeout);

    public void Terminate()
    {
        if (!HasExited)
        {
            TerminateJobObject(_job, 1);
        }
    }

    public string DiagnosticsSnapshot()
    {
        lock (_diagnostics)
        {
            return _diagnostics.ToString();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        try
        {
            Terminate();
            _wait.WaitOne(TimeSpan.FromSeconds(AssistVersions.StopDrainSeconds));
        }
        catch (Win32Exception)
        {
        }

        _drain.Cancel();
        _stdout.Dispose();
        _stderr.Dispose();
        _wait.Dispose();
        CloseHandle(ProcessHandle);
        CloseHandle(_job);
        _drain.Dispose();
    }

    private async Task DrainAsync(FileStream stream, CancellationToken cancellationToken)
    {
        var buffer = new byte[1024];
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
                if (read <= 0)
                {
                    break;
                }

                lock (_diagnostics)
                {
                    if (_diagnostics.Length > 4096)
                    {
                        _diagnostics.Remove(0, _diagnostics.Length - 2048);
                    }

                    _diagnostics.Append(Encoding.UTF8.GetString(buffer, 0, read));
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (IOException)
        {
        }
    }

    private static void ApplyKillOnClose(IntPtr job)
    {
        var info = new JobObjectExtendedLimitInformation
        {
            BasicLimitInformation = new JobObjectBasicLimitInformation
            {
                LimitFlags = JobObjectLimitKillOnJobClose
            }
        };
        var size = Marshal.SizeOf<JobObjectExtendedLimitInformation>();
        var buffer = Marshal.AllocHGlobal(size);
        try
        {
            Marshal.StructureToPtr(info, buffer, false);
            if (!SetInformationJobObject(job, JobExtendedLimitInfoClass, buffer, (uint)size))
            {
                throw new Win32Exception(Marshal.GetLastPInvokeError(), "set-job-limit-failed");
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static (SafeFileHandle Read, SafeFileHandle Write) CreatePipePair()
    {
        var sa = new SecurityAttributes
        {
            nLength = Marshal.SizeOf<SecurityAttributes>(),
            bInheritHandle = true
        };
        if (!CreatePipe(out var read, out var write, ref sa, 0))
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError(), "create-pipe-failed");
        }

        if (!SetHandleInformation(read, 1, 0))
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError(), "pipe-read-noinherit-failed");
        }

        return (read, write);
    }

    private static IntPtr BuildEnvironment(IReadOnlyDictionary<string, string> environment)
    {
        var builder = new StringBuilder();
        foreach (var pair in environment.OrderBy(static item => item.Key, StringComparer.OrdinalIgnoreCase))
        {
            builder.Append(pair.Key);
            builder.Append('=');
            builder.Append(pair.Value);
            builder.Append('\0');
        }

        builder.Append('\0');
        var bytes = Encoding.Unicode.GetBytes(builder.ToString());
        var pointer = Marshal.AllocHGlobal(bytes.Length);
        Marshal.Copy(bytes, 0, pointer, bytes.Length);
        return pointer;
    }

    private sealed class ProcessWaitHandle : WaitHandle
    {
        public ProcessWaitHandle(IntPtr process)
        {
            SafeWaitHandle = new SafeWaitHandle(process, ownsHandle: false);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SecurityAttributes
    {
        public int nLength;
        public IntPtr lpSecurityDescriptor;
        public bool bInheritHandle;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct StartupInfo
    {
        public int cb;
        public IntPtr lpReserved;
        public IntPtr lpDesktop;
        public IntPtr lpTitle;
        public int dwX;
        public int dwY;
        public int dwXSize;
        public int dwYSize;
        public int dwXCountChars;
        public int dwYCountChars;
        public int dwFillAttribute;
        public uint dwFlags;
        public short wShowWindow;
        public short cbReserved2;
        public IntPtr lpReserved2;
        public IntPtr hStdInput;
        public IntPtr hStdOutput;
        public IntPtr hStdError;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct StartupInfoEx
    {
        public StartupInfo StartupInfo;
        public IntPtr lpAttributeList;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessInformation
    {
        public IntPtr hProcess;
        public IntPtr hThread;
        public int dwProcessId;
        public int dwThreadId;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JobObjectBasicLimitInformation
    {
        public long PerProcessUserTimeLimit;
        public long PerJobUserTimeLimit;
        public uint LimitFlags;
        public UIntPtr MinimumWorkingSetSize;
        public UIntPtr MaximumWorkingSetSize;
        public uint ActiveProcessLimit;
        public UIntPtr Affinity;
        public uint PriorityClass;
        public uint SchedulingClass;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IoCounters
    {
        public ulong ReadOperationCount;
        public ulong WriteOperationCount;
        public ulong OtherOperationCount;
        public ulong ReadTransferCount;
        public ulong WriteTransferCount;
        public ulong OtherTransferCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JobObjectExtendedLimitInformation
    {
        public JobObjectBasicLimitInformation BasicLimitInformation;
        public IoCounters IoInfo;
        public UIntPtr ProcessMemoryLimit;
        public UIntPtr JobMemoryLimit;
        public UIntPtr PeakProcessMemoryUsed;
        public UIntPtr PeakJobMemoryUsed;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateJobObjectW(IntPtr jobAttributes, string? name);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetInformationJobObject(IntPtr job, uint infoClass, IntPtr info, uint length);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool TerminateJobObject(IntPtr job, uint exitCode);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool InitializeProcThreadAttributeList(IntPtr list, int count, int flags, ref IntPtr size);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool UpdateProcThreadAttribute(
        IntPtr list,
        uint flags,
        nint attribute,
        IntPtr value,
        nuint size,
        IntPtr previous,
        IntPtr returnSize);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern void DeleteProcThreadAttributeList(IntPtr list);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CreateProcessW(
        string applicationName,
        StringBuilder commandLine,
        IntPtr processAttributes,
        IntPtr threadAttributes,
        bool inheritHandles,
        uint creationFlags,
        IntPtr environment,
        string? currentDirectory,
        ref StartupInfoEx startupInfo,
        out ProcessInformation processInformation);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CreatePipe(out SafeFileHandle readPipe, out SafeFileHandle writePipe, ref SecurityAttributes attributes, int size);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetHandleInformation(SafeFileHandle handle, uint mask, uint flags);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr handle);
}
