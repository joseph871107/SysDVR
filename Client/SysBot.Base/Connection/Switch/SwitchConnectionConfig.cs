using System;
using System.Net;
using static SysBot.Base.SwitchProtocol;

namespace SysBot.Base;

/// <summary>
/// Represents the connection details (but not the communication implementation) for the connection.
/// </summary>
public record SwitchConnectionConfig : ISwitchConnectionConfig, IWirelessConnectionConfig
{
    /// <inheritdoc/>
    public SwitchProtocol Protocol { get; set; }

    /// <inheritdoc/>
    public string IP { get; init; } = string.Empty;

    /// <inheritdoc/>
    public int Port { get; init; } = 6000;

    /// <inheritdoc/>
    public bool UseCRLF => Protocol is WiFi;

    /// <inheritdoc/>
    public bool IsValid() => Protocol switch
    {
        WiFi => IPAddress.TryParse(IP, out _),
        USB => Port < ushort.MaxValue,
        _ => false,
    };

    /// <inheritdoc/>
    public bool Matches(string magic) => Protocol switch
    {
        WiFi => IPAddress.TryParse(magic, out var val) && val.ToString() == IP,
        USB => magic == Port.ToString(),
        _ => false,
    };

    public IConsoleBotConfig GetInnerConfig() => this;

    public override string ToString() => Protocol switch
    {
        WiFi => IP,
        USB => Port.ToString(),
        _ => throw new ArgumentOutOfRangeException(nameof(SwitchProtocol)),
    };

    /// <inheritdoc/>
    public ISwitchConnectionAsync CreateAsynchronous() => Protocol switch
    {
        WiFi => SwitchSocketAsync.CreateInstance(this),
        _ => throw new ArgumentOutOfRangeException(nameof(SwitchProtocol), Protocol, null),
    };

    public ISwitchConnectionSync CreateSync() => Protocol switch
    {
        WiFi => new SwitchSocketSync(this),
        _ => throw new ArgumentOutOfRangeException(nameof(SwitchProtocol), Protocol, null),
    };
}
