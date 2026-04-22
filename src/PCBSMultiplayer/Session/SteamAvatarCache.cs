using System.Collections.Generic;
using Steamworks;
using UnityEngine;

namespace PCBSMultiplayer.Session;

// Static, session-lifetime cache. Textures are small (64x64 RGBA = 16KB),
// SteamIDs are stable, no refcounting needed. Cleared on plugin disable/reload.
public static class SteamAvatarCache
{
    private static readonly Dictionary<ulong, Texture2D> _cache = new();

    // Synchronous best-effort fetch. Returns null if Steam doesn't have the avatar
    // ready yet — caller should fall back to placeholder and register for
    // AvatarImageLoaded_t (see PCBSMultiplayerPlugin.Awake).
    public static Texture2D TryGet(ulong steamId)
    {
        if (_cache.TryGetValue(steamId, out var cached)) return cached;
        int handle = SteamFriends.GetMediumFriendAvatar(new CSteamID(steamId));
        if (handle <= 0) return null;
        var tex = LoadFromHandle(handle);
        if (tex != null) _cache[steamId] = tex;
        return tex;
    }

    // Called from AvatarImageLoaded_t callback.
    public static void StoreFromHandle(ulong steamId, int handle)
    {
        var tex = LoadFromHandle(handle);
        if (tex != null) _cache[steamId] = tex;
    }

    private static Texture2D LoadFromHandle(int handle)
    {
        if (!SteamUtils.GetImageSize(handle, out uint w, out uint h) || w == 0 || h == 0) return null;
        int bytes = (int)(w * h * 4);
        byte[] buf = new byte[bytes];
        if (!SteamUtils.GetImageRGBA(handle, buf, bytes)) return null;
        var tex = new Texture2D((int)w, (int)h, TextureFormat.RGBA32, false);
        tex.LoadRawTextureData(buf);
        tex.Apply(updateMipmaps: false);
        return tex;
    }

    public static void Clear()
    {
        foreach (var tex in _cache.Values) if (tex != null) Object.Destroy(tex);
        _cache.Clear();
    }
}
