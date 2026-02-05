using System;

namespace OpenClaw.Win.Core;

public static class NodesStatusParser
{
    public static bool ContainsNode(string text, NodeIdentity identity)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(identity.Id) &&
            text.IndexOf(identity.Id, StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(identity.DisplayName) &&
            text.IndexOf(identity.DisplayName, StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return true;
        }

        return false;
    }
}
