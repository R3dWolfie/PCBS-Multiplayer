namespace PCBSMultiplayer.Net.Messages;

public sealed class TransformUpdate : IMessage
{
    public byte Slot { get; set; }
    public float PosX { get; set; }
    public float PosY { get; set; }
    public float PosZ { get; set; }
    public float Yaw { get; set; }
    public uint Seq { get; set; }
    public TypeTag Tag => TypeTag.TransformUpdate;
}
