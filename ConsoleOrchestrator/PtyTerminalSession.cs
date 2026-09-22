using System.Text;
using Porta.Pty;
using XTerm;
using XTerm.Options;

namespace ConsoleOrchestrator;

public sealed class PtyTerminalSession : IDisposable
{
    private readonly object _gate = new();
    private readonly SemaphoreSlim _writeGate = new(1, 1);
    private IPtyConnection? _connection;
    private CancellationTokenSource? _pumpCancellation;
    private bool _running;
    private bool _hasOutput;
    private bool _hasStarted;

    public PtyTerminalSession(int cols = 80, int rows = 24)
    {
        Terminal = new XTerm.Terminal(new TerminalOptions
        {
            Cols = Math.Max(2, cols),
            Rows = Math.Max(2, rows),
            Scrollback = 2_000,
            // Invoice emits LF newlines; return to column zero as we advance
            // the row so successive lines do not form a staircase.
            ConvertEol = true,
            TermName = "xterm-256color"
        });
        Terminal.DataReceived += (_, args) => _ = SendAsync(args.Data);
        Terminal.BufferChanged += (_, _) => ScreenChanged?.Invoke();
        Terminal.LineFed += (_, _) => ScreenChanged?.Invoke();
        Terminal.Scrolled += (_, _) => ScreenChanged?.Invoke();
    }

    public XTerm.Terminal Terminal { get; }
    public bool IsRunning { get { lock (_gate) return _running; } }
    public bool HasOutput { get { lock (_gate) return _hasOutput; } }
    public bool HasStarted { get { lock (_gate) return _hasStarted; } }
    public int? ProcessId { get { lock (_gate) return IsRunning ? _connection?.Pid : null; } }

    public event Action? ScreenChanged;
    public event Action? StateChanged;

    public async Task StartAsync(ConsoleAppDefinition definition, int cols, int rows)
    {
        lock (_gate)
        {
            if (_running) return;
            _hasStarted = true;
        }

        string cwd = Path.GetFullPath(definition.WorkingDirectory, Environment.CurrentDirectory);
        string app = ResolveExecutable(definition.FileName, cwd);
        string[] arguments = definition.ArgumentList ?? (string.IsNullOrWhiteSpace(definition.Arguments)
            ? []
            : [definition.Arguments]);

        Terminal.Reset();
        Resize(cols, rows);
        Dictionary<string, string> environment = Environment.GetEnvironmentVariables()
            .Cast<System.Collections.DictionaryEntry>()
            .Where(entry => entry.Key is string && entry.Value is not null)
            .ToDictionary(entry => (string)entry.Key, entry => (string)entry.Value!);
        environment["TERM"] = "xterm-256color";
        environment["COLORTERM"] = "truecolor";

        IPtyConnection connection = await PtyProvider.SpawnAsync(new PtyOptions
        {
            Name = definition.Name,
            Cols = Math.Max(2, cols),
            Rows = Math.Max(2, rows),
            Cwd = cwd,
            App = app,
            CommandLine = arguments,
            Environment = environment
        }, CancellationToken.None).ConfigureAwait(false);

        CancellationTokenSource cancellation = new();
        lock (_gate)
        {
            _connection = connection;
            _pumpCancellation = cancellation;
            _running = true;
            _hasOutput = false;
        }

        connection.ProcessExited += (_, _) =>
        {
            lock (_gate) _running = false;
            StateChanged?.Invoke();
            ScreenChanged?.Invoke();
        };
        _ = PumpAsync(connection, cancellation.Token);
        StateChanged?.Invoke();
    }

    public async Task SendAsync(string text)
    {
        if (string.IsNullOrEmpty(text)) return;
        IPtyConnection? connection;
        lock (_gate) connection = _connection;
        if (connection is null || !IsRunning) return;

        byte[] bytes = Encoding.UTF8.GetBytes(text);
        await _writeGate.WaitAsync().ConfigureAwait(false);
        try
        {
            await connection.WriterStream.WriteAsync(bytes).ConfigureAwait(false);
            await connection.WriterStream.FlushAsync().ConfigureAwait(false);
        }
        catch (IOException) { }
        catch (ObjectDisposedException) { }
        finally { _writeGate.Release(); }
    }

    private static string ResolveExecutable(string name, string cwd)
    {
        if (Path.IsPathRooted(name)) return name;
        if (name.Contains(Path.DirectorySeparatorChar)) return Path.GetFullPath(name, cwd);
        foreach (string directory in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
        {
            string candidate = Path.GetFullPath(Path.Combine(directory, name), cwd);
            if (File.Exists(candidate)) return candidate;
        }
        throw new FileNotFoundException($"Cannot find {name} on PATH. Install the .NET SDK to run source projects.");
    }

    public void Resize(int cols, int rows)
    {
        cols = Math.Max(2, cols);
        rows = Math.Max(2, rows);
        if (Terminal.Cols != cols || Terminal.Rows != rows) Terminal.Resize(cols, rows);
        lock (_gate)
        {
            if (_running && _connection is { } connection) connection.Resize(cols, rows);
        }
    }

    public void Stop()
    {
        IPtyConnection? connection;
        lock (_gate) connection = _connection;
        if (connection is null || !IsRunning) return;
        connection.Kill();
        connection.WaitForExit(3_000);
        lock (_gate) _running = false;
        StateChanged?.Invoke();
    }

    private async Task PumpAsync(IPtyConnection connection, CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[8_192];
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                int count = await connection.ReaderStream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
                if (count == 0) break;
                lock (Terminal) Terminal.Write(buffer.AsSpan(0, count));
                lock (_gate) _hasOutput = true;
                ScreenChanged?.Invoke();
            }
        }
        catch (OperationCanceledException) { }
        catch (IOException) { }
        catch (ObjectDisposedException) { }
    }

    public void Dispose()
    {
        Stop();
        lock (_gate)
        {
            _pumpCancellation?.Cancel();
            _pumpCancellation?.Dispose();
            _connection?.Dispose();
            _connection = null;
            _running = false;
        }
        _writeGate.Dispose();
        Terminal.Dispose();
    }
}
