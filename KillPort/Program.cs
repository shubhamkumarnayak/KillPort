using System.Diagnostics;
using System.Runtime.InteropServices;

Console.WriteLine("KillPort Utility");
Console.WriteLine("================");
Console.WriteLine("Enter one port or comma-separated ports.");
Console.WriteLine("Example: 3000 or 3000,5000,5173");
Console.WriteLine("Search process names with: node or name:node");
Console.WriteLine("Type 'exit' or 'quit' to close.");
Console.WriteLine();

if (args.Length > 0)
{
    ProcessArgs(args);
    return;
}

while (true)
{
    Console.WriteLine();
    Console.Write("Enter port(s) or process name to kill: ");

    var input = Console.ReadLine();

    if (input is null)
    {
        Console.WriteLine("Exiting KillPort Utility.");
        break;
    }

    if (string.IsNullOrWhiteSpace(input))
    {
        Console.WriteLine("[SKIPPED] No input entered.");
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

static void ProcessArgs(string[] args)
{
    if (args.Length >= 2 &&
        (args[0].Equals("--name", StringComparison.OrdinalIgnoreCase) ||
         args[0].Equals("-n", StringComparison.OrdinalIgnoreCase)))
    {
        KillProcessesByName(string.Join(' ', args.Skip(1)));
        return;
    }

    ProcessInput(string.Join(' ', args));
}

static void ProcessInput(string input)
{
    if (TryGetProcessNameInput(input, out var processName))
    {
        KillProcessesByName(processName);
        return;
    }

    if (ShouldSearchByProcessName(input))
    {
        KillProcessesByName(input);
        return;
    }

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
            Console.WriteLine($"[INVALID] '{portResult.RawValue}' is not a valid port or process name.");
            continue;
        }

        KillProcessUsingPort(portResult.Port);
    }
}

static bool ShouldSearchByProcessName(string input)
{
    return !input.Contains(',') &&
        input.Any(character => !char.IsDigit(character));
}

static bool TryGetProcessNameInput(string input, out string processName)
{
    const string processNamePrefix = "name:";

    processName = string.Empty;

    if (!input.StartsWith(processNamePrefix, StringComparison.OrdinalIgnoreCase))
    {
        return false;
    }

    processName = input[processNamePrefix.Length..].Trim();

    if (string.IsNullOrWhiteSpace(processName))
    {
        Console.WriteLine("[INVALID] Process name cannot be empty.");
        return true;
    }

    return true;
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

static void KillProcessesByName(string processName)
{
    var searchText = NormalizeProcessName(processName);

    if (string.IsNullOrWhiteSpace(searchText))
    {
        Console.WriteLine("[INVALID] Process name cannot be empty.");
        return;
    }

    Console.WriteLine();
    Console.WriteLine($"Searching process names for '{searchText}'...");

    try
    {
        var matches = FindProcessesByName(searchText);

        if (matches.Count == 0)
        {
            Console.WriteLine($"[OK] No process found matching '{searchText}'.");
            return;
        }

        var selectedProcesses = SelectProcessesToKill(matches);

        if (selectedProcesses.Count == 0)
        {
            Console.WriteLine("[SKIPPED] No process selected.");
            DisposeProcesses(matches);
            return;
        }

        foreach (var process in selectedProcesses)
        {
            KillProcess(process, printFound: false);
        }

        DisposeProcesses(matches);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[ERROR] Failed while checking process name '{searchText}'.");
        Console.WriteLine($"Reason: {ex.Message}");
    }
}

static List<Process> FindProcessesByName(string searchText)
{
    return Process.GetProcesses()
        .Where(process => ProcessNameContains(process, searchText))
        .OrderBy(process => process.ProcessName, StringComparer.OrdinalIgnoreCase)
        .ThenBy(process => process.Id)
        .ToList();
}

static bool ProcessNameContains(Process process, string searchText)
{
    try
    {
        return process.ProcessName.Contains(searchText, StringComparison.OrdinalIgnoreCase);
    }
    catch
    {
        return false;
    }
}

static List<Process> SelectProcessesToKill(List<Process> matches)
{
    Console.WriteLine("[FOUND] Matching processes:");

    for (var i = 0; i < matches.Count; i++)
    {
        var process = matches[i];
        Console.WriteLine($"  {i + 1}. PID {process.Id} ({process.ProcessName})");
    }

    Console.Write("Select process number(s), 'all', or press Enter to skip: ");
    var input = CleanInput(Console.ReadLine());

    if (string.IsNullOrWhiteSpace(input))
    {
        return new List<Process>();
    }

    if (input.Equals("all", StringComparison.OrdinalIgnoreCase) ||
        input.Equals("a", StringComparison.OrdinalIgnoreCase))
    {
        return matches;
    }

    var selectedIndexes = new HashSet<int>();

    foreach (var value in input.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
    {
        if (!int.TryParse(value, out var selectedNumber) ||
            selectedNumber < 1 ||
            selectedNumber > matches.Count)
        {
            Console.WriteLine($"[INVALID] '{value}' is not a valid selection.");
            return new List<Process>();
        }

        selectedIndexes.Add(selectedNumber - 1);
    }

    return selectedIndexes
        .Order()
        .Select(index => matches[index])
        .ToList();
}

static void DisposeProcesses(List<Process> processes)
{
    foreach (var process in processes)
    {
        process.Dispose();
    }
}

static string CleanInput(string? input)
{
    return input?.Trim().Trim('\uFEFF') ?? string.Empty;
}

static void KillProcess(Process process, bool printFound = true)
{
    int pid;
    string processName;

    try
    {
        pid = process.Id;
        processName = process.ProcessName;
    }
    catch (Exception ex)
    {
        Console.WriteLine("[SKIPPED] Could not read process details.");
        Console.WriteLine($"Reason: {ex.Message}");
        return;
    }

    if (printFound)
    {
        Console.WriteLine($"[FOUND] PID {pid} ({processName}).");
    }

    try
    {
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

static string NormalizeProcessName(string processName)
{
    processName = processName.Trim().Trim('"');

    if (processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
    {
        processName = processName[..^4];
    }

    return processName;
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

                KillProcess(process);
            }
            catch (ArgumentException)
            {
                Console.WriteLine($"[SKIPPED] PID {pid} no longer exists.");
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
