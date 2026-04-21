namespace PCBSMultiplayer.State;

// Pure data class — no UnityEngine types. Renderer (RemotePlayerRenderer) translates
// PosX/Y/Z to Vector3 at render time. Capsule + CapsuleMat fields are set by the
// renderer as `object` and cast back — same pattern keeps xUnit free of UnityEngine.
public sealed class RemotePlayer
{
    public int Slot;
    public ulong SteamId;
    public string DisplayName = "";
    public object Avatar;          // UnityEngine.Texture2D at runtime; null until loaded
    public float PosX, PosY, PosZ;
    public float PrevPosX, PrevPosY, PrevPosZ;
    public float Yaw;
    public float PrevYaw;
    public long LastSampleMs;
    public long LastSeenMs;
    public uint LastSeq;
    public object Capsule;         // UnityEngine.GameObject at runtime
    public object CapsuleMat;      // UnityEngine.Material at runtime
}
