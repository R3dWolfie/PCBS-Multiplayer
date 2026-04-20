using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using PCBSMultiplayer.Net.Messages;
using PCBSMultiplayer.State;

namespace PCBSMultiplayer.Net;

public static class Serializer
{
    public static byte[] Pack(IMessage message)
    {
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms, Encoding.UTF8);
        w.Write((byte)message.Tag);
        WritePayload(w, message);
        w.Flush();
        return ms.ToArray();
    }

    public static (TypeTag tag, IMessage message) Unpack(byte[] framed)
    {
        if (framed.Length < 1) throw new ArgumentException("frame too short");
        using var ms = new MemoryStream(framed, false);
        using var r = new BinaryReader(ms, Encoding.UTF8);
        var tag = (TypeTag)r.ReadByte();
        return (tag, ReadPayload(r, tag));
    }

    private static void WritePayload(BinaryWriter w, IMessage m)
    {
        switch (m)
        {
            case Heartbeat x: w.Write(x.SentAtMs); break;
            case Hello x:
                w.Write(x.ModVersion);
                w.Write(x.GameVersion);
                w.Write(x.SteamId);
                w.Write(x.DisplayName);
                break;
            case Welcome x:
                w.Write(x.AssignedSlot);
                w.Write(x.SnapshotBytes.Length);
                w.Write(x.SnapshotBytes);
                break;
            case Bye x: w.Write(x.Reason); break;
            case MoneyChanged x: w.Write(x.NewTotal); break;
            case XPChanged x: w.Write(x.NewTotal); break;
            case TimeChanged x: w.Write(x.NewDayIndex); break;
            case JobBoardDelta x:
                WriteJobList(w, x.Available);
                WriteJobList(w, x.Claimed);
                WriteJobList(w, x.Completed);
                break;
            case ClaimJobRequest x:
                w.Write(x.JobId);
                w.Write(x.RequestId);
                break;
            case SpendMoneyRequest x:
                w.Write(x.Amount);
                w.Write(x.Reason);
                w.Write(x.RequestId);
                break;
            case ClaimJobResult x:
                w.Write(x.RequestId);
                w.Write(x.Accepted);
                w.Write(x.DenyReason);
                break;
            case SpendMoneyResult x:
                w.Write(x.RequestId);
                w.Write(x.Accepted);
                w.Write(x.DenyReason);
                break;
            default: throw new NotSupportedException($"no writer for {m.GetType()}");
        }
    }

    private static IMessage ReadPayload(BinaryReader r, TypeTag tag) => tag switch
    {
        TypeTag.Heartbeat => new Heartbeat { SentAtMs = r.ReadInt64() },
        TypeTag.Hello => new Hello
        {
            ModVersion = r.ReadString(),
            GameVersion = r.ReadString(),
            SteamId = r.ReadUInt64(),
            DisplayName = r.ReadString()
        },
        TypeTag.Welcome => ReadWelcome(r),
        TypeTag.Bye => new Bye { Reason = r.ReadString() },
        TypeTag.MoneyChanged => new MoneyChanged { NewTotal = r.ReadInt64() },
        TypeTag.XPChanged => new XPChanged { NewTotal = r.ReadInt64() },
        TypeTag.TimeChanged => new TimeChanged { NewDayIndex = r.ReadInt32() },
        TypeTag.JobBoardDelta => new JobBoardDelta
        {
            Available = ReadJobList(r),
            Claimed = ReadJobList(r),
            Completed = ReadJobList(r)
        },
        TypeTag.ClaimJobRequest => new ClaimJobRequest
        {
            JobId = r.ReadString(),
            RequestId = r.ReadString()
        },
        TypeTag.SpendMoneyRequest => new SpendMoneyRequest
        {
            Amount = r.ReadInt64(),
            Reason = r.ReadString(),
            RequestId = r.ReadString()
        },
        TypeTag.ClaimJobResult => new ClaimJobResult
        {
            RequestId = r.ReadString(),
            Accepted = r.ReadBoolean(),
            DenyReason = r.ReadString()
        },
        TypeTag.SpendMoneyResult => new SpendMoneyResult
        {
            RequestId = r.ReadString(),
            Accepted = r.ReadBoolean(),
            DenyReason = r.ReadString()
        },
        _ => throw new NotSupportedException($"no reader for tag {tag}")
    };

    private static Welcome ReadWelcome(BinaryReader r)
    {
        var slot = r.ReadInt32();
        var len = r.ReadInt32();
        return new Welcome { AssignedSlot = slot, SnapshotBytes = r.ReadBytes(len) };
    }

    private static void WriteJobList(BinaryWriter w, List<SnapshotBuilder.JobDto> list)
    {
        w.Write(list.Count);
        foreach (var j in list) { w.Write(j.Id); w.Write(j.ClaimedBySlot); }
    }

    private static List<SnapshotBuilder.JobDto> ReadJobList(BinaryReader r)
    {
        var n = r.ReadInt32();
        var list = new List<SnapshotBuilder.JobDto>(n);
        for (int i = 0; i < n; i++)
            list.Add(new SnapshotBuilder.JobDto { Id = r.ReadString(), ClaimedBySlot = r.ReadInt32() });
        return list;
    }
}
