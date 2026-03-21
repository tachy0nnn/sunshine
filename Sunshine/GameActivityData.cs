using System;

namespace Sunshine;

public class GameActivityData
{
    public long PlaceId { get; set; }
    public string JobId { get; set; } = "";
    public string MachineAddress { get; set; } = "";
    public DateTime TimeJoined { get; set; }
    public DateTime? TimeLeft { get; set; }
    public bool IsTeleport { get; set; }

    public override string ToString()
    {
        return $"{PlaceId}/{JobId}";
    }
}