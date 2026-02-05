using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using OpenClaw.Win.Core;

namespace OpenClaw.Win.Core.Tests;

internal sealed class FakeConfigStore : IConfigStore
{
    public bool Exists { get; set; } = true;
    public AppConfig Config { get; set; } = new();

    public AppConfig Load() => Config;

    public void Save(AppConfig config) => Config = config;
}

internal sealed class FakeTokenStore : ITokenStore
{
    public string? Token { get; set; }
    public bool HasToken => !string.IsNullOrWhiteSpace(Token);
    public string? LoadToken() => Token;
    public void SaveToken(string token) => Token = token;
}

internal sealed class FakeCliLocator : IOpenClawCliLocator
{
    public string? Path { get; set; } = "openclaw";
    public Task<string?> FindAsync() => Task.FromResult(Path);
}

internal sealed class FakeProcessRunner : IProcessRunner
{
    public List<(string FileName, string Arguments)> Calls { get; } = new();
    public Dictionary<string, ProcessResult> Results { get; } = new(StringComparer.OrdinalIgnoreCase);
    public ProcessResult DefaultResult { get; set; } = new() { ExitCode = 0 };

    public Task<ProcessResult> RunAsync(
        string fileName,
        string arguments,
        TimeSpan timeout,
        string? workingDirectory = null,
        IDictionary<string, string?>? environment = null,
        CancellationToken cancellationToken = default)
    {
        Calls.Add((fileName, arguments));
        return Task.FromResult(Results.TryGetValue(arguments, out var result) ? result : DefaultResult);
    }
}

internal sealed class FakeNodeProcessManager : INodeProcessManager
{
    public IReadOnlyList<int> ForegroundIds { get; set; } = Array.Empty<int>();
    public int KillCalls { get; private set; }

    public IReadOnlyList<int> FindForegroundNodeProcessIds() => ForegroundIds;

    public int KillForegroundNodeProcesses()
    {
        KillCalls++;
        return ForegroundIds.Count;
    }
}

internal sealed class FakeNodeHostRunner : INodeHostRunner
{
    public bool IsRunning { get; set; }
    public int StartCalls { get; private set; }
    public int StopCalls { get; private set; }
    public string? LastCliPath { get; private set; }
    public string? LastRunArgs { get; private set; }
    public string? LastToken { get; private set; }
    public bool StartResult { get; set; } = true;

    public bool Start(string cliPath, string runArgs, string? token)
    {
        StartCalls++;
        LastCliPath = cliPath;
        LastRunArgs = runArgs;
        LastToken = token;
        IsRunning = StartResult;
        return StartResult;
    }

    public void Stop()
    {
        StopCalls++;
        IsRunning = false;
    }
}

internal sealed class FakeCanvasRenderer : ICanvasRenderer
{
    public CanvasStatus Status { get; private set; } = CanvasStatus.Ready;
    public event EventHandler<CanvasStatus>? StatusChanged;
    public CanvasContent? LastContent { get; private set; }
    public string? LastScript { get; private set; }
    public CanvasEvalResult EvalResult { get; set; } = new(true, "ok", null);
    public CanvasSnapshotResult SnapshotResult { get; set; } = new(true, Array.Empty<byte>(), null);

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        Status = CanvasStatus.Ready;
        StatusChanged?.Invoke(this, Status);
        return Task.CompletedTask;
    }

    public Task RenderAsync(CanvasContent content, CancellationToken cancellationToken = default)
    {
        LastContent = content;
        return Task.CompletedTask;
    }

    public Task<CanvasEvalResult> EvalAsync(string script, CancellationToken cancellationToken = default)
    {
        LastScript = script;
        return Task.FromResult(EvalResult);
    }

    public Task<CanvasSnapshotResult> CaptureSnapshotAsync(CanvasSnapshotOptions? options = null, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(SnapshotResult);
    }
}

internal sealed class TempStateDir : IDisposable
{
    private const string OverrideEnvVar = "OPENCLAW_COMPANION_STATE_DIR";
    private const string OpenClawStateEnvVar = "OPENCLAW_STATE_DIR";
    private readonly string? _previous;
    private readonly string? _previousState;
    public string Path { get; }

    public TempStateDir()
    {
        _previous = Environment.GetEnvironmentVariable(OverrideEnvVar);
        _previousState = Environment.GetEnvironmentVariable(OpenClawStateEnvVar);
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
        Environment.SetEnvironmentVariable(OverrideEnvVar, Path);
        Environment.SetEnvironmentVariable(OpenClawStateEnvVar, Path);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(OverrideEnvVar, _previous);
        Environment.SetEnvironmentVariable(OpenClawStateEnvVar, _previousState);
        if (Directory.Exists(Path))
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}
