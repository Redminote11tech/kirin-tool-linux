namespace Kirin_Tool.Cli;

/// <summary>Stable CLI exit codes (documented in docs/cli-mcp.md).</summary>
internal static class ExitCodes
{
    public const int Ok = 0;
    public const int Usage = 1;
    public const int NoDevice = 2;
    public const int DeviceRejected = 3;
    public const int Gated = 4;
    public const int FileOrVerify = 5;
}
