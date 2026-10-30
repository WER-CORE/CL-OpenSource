using System;

namespace CL.Core.Models.P2P;

public class P2PRoomSession
{
    public string RoomCode { get; set; } = string.Empty;
    public string HostAddress { get; set; } = string.Empty;
    public int PublicPort { get; set; }
    public int LocalPort { get; set; }
    public string WorldName { get; set; } = string.Empty;
    public string GameVersion { get; set; } = string.Empty;
    public string LoaderType { get; set; } = string.Empty;
    public string LoaderVersion { get; set; } = string.Empty;
    public string ModpackName { get; set; } = string.Empty;
    public string ModpackTypeSite { get; set; } = string.Empty;
    public string SessionSecret { get; set; } = string.Empty;
    public bool RequireApproval { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; }
}
