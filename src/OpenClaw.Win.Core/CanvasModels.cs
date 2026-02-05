using System;
using System.Threading;
using System.Threading.Tasks;

namespace OpenClaw.Win.Core;

public enum CanvasStatus
{
    Uninitialized,
    Ready,
    MissingRuntime,
    Error
}

public sealed record CanvasContent(string Html, string? Title = null, string? ContentType = "text/html");

public sealed record CanvasEvalResult(bool Success, string? Result, string? Error);

public sealed record CanvasSnapshotOptions(int? Width = null, int? Height = null);

public sealed record CanvasSnapshotResult(bool Success, byte[]? Data, string? Error);

public interface ICanvasRenderer
{
    CanvasStatus Status { get; }
    event EventHandler<CanvasStatus>? StatusChanged;
    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task RenderAsync(CanvasContent content, CancellationToken cancellationToken = default);
    Task<CanvasEvalResult> EvalAsync(string script, CancellationToken cancellationToken = default);
    Task<CanvasSnapshotResult> CaptureSnapshotAsync(CanvasSnapshotOptions? options = null, CancellationToken cancellationToken = default);
}
