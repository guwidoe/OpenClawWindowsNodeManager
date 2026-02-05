using System;

namespace OpenClaw.Win.Core;

public sealed class StatusStabilizer
{
    private readonly int _threshold;
    private NodeStatus? _stable;
    private NodeConnectionState? _pendingState;
    private int _pendingCount;

    public StatusStabilizer(int threshold = 2)
    {
        if (threshold < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(threshold), "Threshold must be at least 1.");
        }

        _threshold = threshold;
    }

    public NodeStatus Stabilize(NodeStatus current)
    {
        if (_stable == null)
        {
            _stable = current;
            return current;
        }

        var currentState = current.ToConnectionState();
        var stableState = _stable.ToConnectionState();

        if (currentState == stableState)
        {
            _pendingState = null;
            _pendingCount = 0;
            _stable = current;
            return current;
        }

        if (_pendingState == currentState)
        {
            _pendingCount++;
        }
        else
        {
            _pendingState = currentState;
            _pendingCount = 1;
        }

        if (_pendingCount >= _threshold)
        {
            _stable = current;
            _pendingState = null;
            _pendingCount = 0;
            return current;
        }

        return _stable;
    }

    public void Reset()
    {
        _stable = null;
        _pendingState = null;
        _pendingCount = 0;
    }
}
