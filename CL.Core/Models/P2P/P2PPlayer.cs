using System;

namespace CL.Core.Models.P2P;

public class P2PPlayer
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Username { get; set; } = string.Empty;
    public string PlayerName => Username;
    public string Uuid { get; set; } = string.Empty;
    public int PingMs { get; set; }
    public DateTime ConnectedAt { get; set; } = DateTime.UtcNow;
    public string ConnectionId { get; set; } = string.Empty;
    public string SessionDurationText => (DateTime.UtcNow - ConnectedAt).ToString(@"mm\:ss");
}
