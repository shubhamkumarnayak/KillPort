# KillPort

Small cross-platform .NET CLI utility to find and terminate the process listening on a TCP port.

## Description

KillPort locates the process bound to a given TCP port and attempts to terminate it. Useful when a stray process blocks a port needed for local development.

## Prerequisites

- .NET 10 SDK (https://dotnet.microsoft.com)
- On Windows: running the command prompt / PowerShell with Administrator privileges may be required to terminate some processes.

## Build

From the repository root:

- Restore & build
  dotnet build

- Run directly
  dotnet run --project KillPort -- <PORT>

- Publish a self-contained executable (example for Windows x64)
  dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o ./publish

## Usage

- Using dotnet run
  dotnet run --project KillPort -- 8080

- Using a published executable
  ./publish/KillPort.exe 8080

The tool will print information about the process it found (PID, name) and whether termination succeeded. It attempts a graceful close, then a forceful kill if needed.

## Safety

- Verify the PID shown before confirming termination to avoid stopping unrelated system services.
- You may need elevated privileges to kill some processes.

## Contributing

Issues and pull requests are welcome. Keep changes small and include tests when applicable.

## License

MIT
