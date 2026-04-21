using System;
using System.Collections.Generic;
using BepInEx.Logging;
using PCBSMultiplayer.Net.Messages;

namespace PCBSMultiplayer.Net;

public sealed class MessageRouter
{
    private static readonly ManualLogSource Log = BepInEx.Logging.Logger.CreateLogSource("PCBSMultiplayer.Router");

    private readonly Dictionary<Type, Action<IMessage>> _handlers = new();

    public void On<T>(Action<T> handler) where T : IMessage
    {
        _handlers[typeof(T)] = m => handler((T)m);
    }

    public void Dispatch(byte[] frame)
    {
        try
        {
            // out-param form avoids ValueTuple<,> in IL — Mono 2018 can't JIT tuple deconstruction.
            var msg = Serializer.Unpack(frame, out _);
            if (_handlers.TryGetValue(msg.GetType(), out var h)) h(msg);
        }
        catch (NotSupportedException) { /* unknown tag — drop */ }
        catch (System.IO.EndOfStreamException) { /* malformed — drop */ }
        catch (System.IO.IOException) { /* malformed — drop */ }
        catch (Exception ex)
        {
            // Previously swallowed silently — handler exceptions that weren't one of the three
            // above would propagate and could leave the session in a partial state (e.g. OnWelcome
            // throws before IsLive flips true → client sits locally-playing for the whole session).
            Log.LogError("Handler threw: " + ex.Message + "\n" + ex.StackTrace);
        }
    }
}
