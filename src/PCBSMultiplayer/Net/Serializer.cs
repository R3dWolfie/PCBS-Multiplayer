using System;
using MessagePack;
using PCBSMultiplayer.Net.Messages;

namespace PCBSMultiplayer.Net;

public static class Serializer
{
    public static byte[] Pack(IMessage message)
    {
        var payload = MessagePackSerializer.Serialize(message.GetType(), message);
        var framed = new byte[1 + payload.Length];
        framed[0] = (byte)message.Tag;
        Buffer.BlockCopy(payload, 0, framed, 1, payload.Length);
        return framed;
    }

    public static (TypeTag tag, IMessage message) Unpack(byte[] framed)
    {
        if (framed.Length < 1) throw new ArgumentException("frame too short");
        var tag = (TypeTag)framed[0];
        var payload = new byte[framed.Length - 1];
        Buffer.BlockCopy(framed, 1, payload, 0, payload.Length);
        var type = TagToType(tag);
        var obj = (IMessage)MessagePackSerializer.Deserialize(type, payload)!;
        return (tag, obj);
    }

    private static Type TagToType(TypeTag tag) => tag switch
    {
        TypeTag.Hello => typeof(Hello),
        TypeTag.Welcome => typeof(Welcome),
        TypeTag.Heartbeat => typeof(Heartbeat),
        TypeTag.Bye => typeof(Bye),
        TypeTag.MoneyChanged => typeof(MoneyChanged),
        TypeTag.XPChanged => typeof(XPChanged),
        TypeTag.TimeChanged => typeof(TimeChanged),
        TypeTag.JobBoardDelta => typeof(JobBoardDelta),
        TypeTag.ClaimJobRequest => typeof(ClaimJobRequest),
        TypeTag.SpendMoneyRequest => typeof(SpendMoneyRequest),
        TypeTag.ClaimJobResult => typeof(ClaimJobResult),
        TypeTag.SpendMoneyResult => typeof(SpendMoneyResult),
        _ => throw new NotSupportedException($"no type mapping for tag {tag}")
    };
}
