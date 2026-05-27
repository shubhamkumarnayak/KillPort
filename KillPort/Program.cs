using System.Diagnostics;
using System.Runtime.InteropServices;

Console.WriteLine("KillPort Utility");
Console.WriteLine("================");
Console.WriteLine("Enter one port or comma-separated ports.");
Console.WriteLine("Example: 3000 or 3000,5000,5173");
Console.WriteLine("Type 'exit' or 'quit' to close.");
Console.WriteLine();

if (args.Length > 0 && !string.IsNullOrWhiteSpace(args[0]))
{
    ProcessInput(args[0]);
}

while (true)
{
    Console.WriteLine();
    Console.Write("Enter port(s) to kill: ");

    var input = Console.ReadLine();

    if (string.IsNullOrWhiteSpace(input))
    {
        Console.WriteLine("[SKIPPED] No port entered.");
        continue;
    }

    input = input.Trim();

    if (input.Equals("exit", StringComparison.OrdinalIgnoreCase) ||
        input.Equals("quit", StringComparison.OrdinalIgnoreCase))
    {
        Console.WriteLine("Exiting KillPort Utility.");
        break;
    }

    ProcessInput(input);
}

static void ProcessInput(string input)
{
    var ports = input
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(ParsePort)
        .ToList();

    if (ports.Count == 0)
    {
        Console.WriteLine("[SKIPPED] No valid input found.");
        return;
    }

    foreach (var portResult in ports)
    {
        if (!portResult.IsValid)
        {
            Console.WriteLine($"[INVALID] '{portResult.RawValue}' is not a valid port number.");
            continue;
        }

        KillProcessUsingPort(portResult.Port);
    }
}

static PortParseResult ParsePort(string value)
{
    if (!int.TryParse(value, out var port))
    {
        return PortParseResult.Invalid(value);
    }

    if (port < 1 || port > 65535)
    {
        return PortParseResult.Invalid(value);
    }

    return PortParseResult.Valid(port);
}

static void KillProcessUsingPort(int port)
{
    Console.WriteLine();
    Console.WriteLine($"Checking port {port}...");

    try
    {
        var processIds = GetProcessIdsUsingPort(port);

        if (processIds.Count == 0)
        {
            Console.WriteLine($"[OK] No process found using port {port}.");
            return;
        }

        foreach (var pid in processIds.Distinct())
        {
            try
            {
                using var process = Process.GetProcessById(pid);
                var processName = process.ProcessName;

                Console.WriteLine($"[FOUND] Port {port} is used by PID {pid} ({processName}).");

                process.Kill(entireProcessTree: true);

                if (process.WaitForExit(5000))
                {
                    Console.WriteLine($"[KILLED] PID {pid} ({processName}) killed successfully.");
                }
                else
                {
                    Console.WriteLine($"[WARNING] Kill signal sent to PID {pid}, but it did not exit within 5 seconds.");
                }
            }
            catch (ArgumentException)
            {
                Console.WriteLine($"[SKIPPED] PID {pid} no longer exists.");
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                Console.WriteLine($"[ACCESS DENIED] Could not kill PID {pid}. Try running as Administrator.");
                Console.WriteLine($"Reason: {ex.Message}");
            }
            catch (InvalidOperationException ex)
            {
                Console.WriteLine($"[FAILED] Could not kill PID {pid}.");
                Console.WriteLine($"Reason: {ex.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Unexpected error while killing PID {pid}.");
                Console.WriteLine($"Reason: {ex.Message}");
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[ERROR] Failed while checking port {port}.");
        Console.WriteLine($"Reason: {ex.Message}");
    }
}

static List<int> GetProcessIdsUsingPort(int port)
{
    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
    {
        return GetProcessIdsUsingPortWindows(port);
    }

    if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ||
        RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
    {
        return GetProcessIdsUsingPortUnix(port);
    }

    throw new PlatformNotSupportedException("This operating system is not supported.");
}

static List<int> GetProcessIdsUsingPortWindows(int port)
{
    var result = RunCommand("netstat", "-ano");

    if (result.ExitCode != 0)
    {
        throw new InvalidOperationException($"netstat failed: {result.Error}");
    }

    var pids = new List<int>();

    foreach (var line in result.Output.Split(Environment.NewLine))
    {
        var trimmed = line.Trim();

        if (string.IsNullOrWhiteSpace(trimmed))
            continue;

        var isTcp = trimmed.StartsWith("TCP", StringComparison.OrdinalIgnoreCase);
        var isUdp = trimmed.StartsWith("UDP", StringComparison.OrdinalIgnoreCase);

        if (!isTcp && !isUdp)
            continue;

        var parts = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (isTcp && parts.Length < 5)
            continue;

        if (isUdp && parts.Length < 4)
            continue;

        var localAddress = parts[1];

        if (!IsMatchingPort(localAddress, port))
            continue;

        var pidText = parts[^1];

        if (int.TryParse(pidText, out var pid))
        {
            pids.Add(pid);
        }
    }

    return pids;
}

static List<int> GetProcessIdsUsingPortUnix(int port)
{
    var result = RunCommand("lsof", $"-ti :{port}");

    if (result.ExitCode != 0 && string.IsNullOrWhiteSpace(result.Output))
    {
        return new List<int>();
    }

    return result.Output
        .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Where(x => int.TryParse(x, out _))
        .Select(int.Parse)
        .ToList();
}

static bool IsMatchingPort(string localAddress, int port)
{
    return localAddress.EndsWith($":{port}", StringComparison.OrdinalIgnoreCase);
}

static CommandResult RunCommand(string fileName, string arguments)
{
    try
    {
        using var process = new Process();

        process.StartInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        process.Start();

        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();

        process.WaitForExit();

        return new CommandResult(process.ExitCode, output, error);
    }
    catch (Exception ex)
    {
        return new CommandResult(-1, string.Empty, ex.Message);
    }
}

record PortParseResult(string RawValue, int Port, bool IsValid)
{
    public static PortParseResult Valid(int port) => new(port.ToString(), port, true);

    public static PortParseResult Invalid(string rawValue) => new(rawValue, 0, false);
}

record CommandResult(int ExitCode, string Output, string Error);