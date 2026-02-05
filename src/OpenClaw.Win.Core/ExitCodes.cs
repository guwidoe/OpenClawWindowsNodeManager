namespace OpenClaw.Win.Core;

public static class ExitCodes
{
    public const int Success = 0;
    public const int GenericFailure = 1;
    public const int Disconnected = 2;
    public const int Degraded = 3;
    public const int ConfigMissing = 10;
    public const int OpenClawMissing = 11;
    public const int AuthTokenError = 12;
    public const int PairingRequired = 13;
}
