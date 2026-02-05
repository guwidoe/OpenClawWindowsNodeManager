using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace OpenClaw.Win.Core;

public interface IConfigStore
{
    bool Exists { get; }
    AppConfig Load();
    void Save(AppConfig config);
}

public interface ITokenStore
{
    bool HasToken { get; }
    string? LoadToken();
    void SaveToken(string token);
}

public interface IOpenClawCliLocator
{
    Task<string?> FindAsync();
}

public interface IProcessRunner
{
    Task<ProcessResult> RunAsync(
        string fileName,
        string arguments,
        TimeSpan timeout,
        string? workingDirectory = null,
        IDictionary<string, string?>? environment = null,
        CancellationToken cancellationToken = default);
}

public interface INodeProcessManager
{
    IReadOnlyList<int> FindForegroundNodeProcessIds();
    int KillForegroundNodeProcesses();
}

public interface INodeHostRunner
{
    bool IsRunning { get; }
    bool Start(string cliPath, string runArgs, string? token);
    void Stop();
}

public sealed class DefaultProcessRunner : IProcessRunner
{
    public Task<ProcessResult> RunAsync(
        string fileName,
        string arguments,
        TimeSpan timeout,
        string? workingDirectory = null,
        IDictionary<string, string?>? environment = null,
        CancellationToken cancellationToken = default)
        => ProcessRunner.RunAsync(fileName, arguments, timeout, workingDirectory, environment, cancellationToken);
}

public sealed class DefaultNodeProcessManager : INodeProcessManager
{
    public IReadOnlyList<int> FindForegroundNodeProcessIds() => NodeProcessManager.FindForegroundNodeProcessIds();

    public int KillForegroundNodeProcesses() => NodeProcessManager.KillForegroundNodeProcesses();
}
