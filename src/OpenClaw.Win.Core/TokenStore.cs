using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace OpenClaw.Win.Core;

public sealed class TokenStore : ITokenStore
{
    public bool HasToken => File.Exists(AppPaths.TokenPath);

    public void SaveToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new ArgumentException("Token cannot be empty.", nameof(token));
        }

        AppPaths.EnsureDirectories();
        var bytes = Encoding.UTF8.GetBytes(token);
        var protectedBytes = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(AppPaths.TokenPath, protectedBytes);
    }

    public string? LoadToken()
    {
        if (!File.Exists(AppPaths.TokenPath))
        {
            return null;
        }

        var protectedBytes = File.ReadAllBytes(AppPaths.TokenPath);
        var bytes = ProtectedData.Unprotect(protectedBytes, null, DataProtectionScope.CurrentUser);
        return Encoding.UTF8.GetString(bytes);
    }

    public void ClearToken()
    {
        if (File.Exists(AppPaths.TokenPath))
        {
            File.Delete(AppPaths.TokenPath);
        }
    }
}
