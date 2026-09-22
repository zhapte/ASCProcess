using System.Diagnostics;

namespace ConsoleOrchestrator;

public sealed class ManagedConsoleApp : IDisposable
{
    private readonly object _gate = new();
    private Process? _process;

    public ManagedConsoleApp(ConsoleAppDefinition definition) => Definition = definition;
    public ConsoleAppDefinition Definition { get; }

    public bool IsRunning
    {
        get { lock (_gate) { return _process is { HasExited: false }; } }
    }

    public int? ProcessId
    {
        get { lock (_gate) { return _process is { HasExited: false } process ? process.Id : null; } }
    }

    public event Action<string>? OutputReceived;
    public event Action? StateChanged;

    public void Start()
    {
        lock (_gate)
        {
            if (_process is { HasExited: false }) return;
            if (!Definition.Enabled)
                throw new InvalidOperationException($"{Definition.Name} is a placeholder. Configure it and set Enabled to true in AppCatalog.cs.");

            string workingDirectory = Path.GetFullPath(Definition.WorkingDirectory, Environment.CurrentDirectory);
            Process process = new()
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = Definition.FileName,
                    Arguments = Definition.Arguments,
                    WorkingDirectory = workingDirectory,
                    UseShellExecute = false,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                },
                EnableRaisingEvents = true
            };

            process.Exited += (_, _) =>
            {
                WriteLine($"Process exited with code {process.ExitCode}.", false);
                StateChanged?.Invoke();
            };

            if (!process.Start())
            {
                process.Dispose();
                throw new InvalidOperationException($"Could not start {Definition.Name}.");
            }

            _process = process;
            _ = PumpStreamAsync(process.StandardOutput, isError: false);
            _ = PumpStreamAsync(process.StandardError, isError: true);
            WriteLine($"Started PID {process.Id}: {Definition.FileName} {Definition.Arguments}", false);
        }
        StateChanged?.Invoke();
    }

    public void Stop()
    {
        Process? process;
        lock (_gate)
        {
            process = _process;
            if (process is null || process.HasExited) return;
        }

        WriteLine("Stopping process...", false);
        process.Kill(entireProcessTree: true);
        process.WaitForExit(3000);
        StateChanged?.Invoke();
    }

    public void Restart() { Stop(); Start(); }

    public void SendInput(string input)
    {
        lock (_gate)
        {
            if (_process is not { HasExited: false } process)
                throw new InvalidOperationException($"Start {Definition.Name} before sending input.");

            process.StandardInput.WriteLine(input);
            process.StandardInput.Flush();
        }

        WriteLine($"> {input}", false);
    }

    private void WriteLine(string? text, bool isError)
    {
        if (text is not null)
            OutputReceived?.Invoke($"[{DateTime.Now:HH:mm:ss}] {(isError ? "ERR " : string.Empty)}{text}{Environment.NewLine}");
    }

    private async Task PumpStreamAsync(StreamReader reader, bool isError)
    {
        char[] buffer = new char[512];
        try
        {
            while (await reader.ReadAsync(buffer).ConfigureAwait(false) is int count and > 0)
            {
                string chunk = new(buffer, 0, count);
                OutputReceived?.Invoke(isError ? $"[ERR] {chunk}" : chunk);
            }
        }
        catch (ObjectDisposedException)
        {
            // Expected when the orchestrator closes a running child process.
        }
        catch (IOException exception)
        {
            WriteLine($"Output stream closed: {exception.Message}", true);
        }
    }

    public void Dispose()
    {
        Stop();
        lock (_gate) { _process?.Dispose(); _process = null; }
    }
}
