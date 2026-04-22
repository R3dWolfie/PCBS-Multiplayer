using BepInEx.Logging;
using PCBSMultiplayer.Session;
using PCBSMultiplayer.State;
using UnityEngine;

namespace PCBSMultiplayer.UI;

public sealed class RemotePlayerRenderer : MonoBehaviour
{
    private static readonly ManualLogSource Log =
        BepInEx.Logging.Logger.CreateLogSource("PCBSMultiplayer.Renderer");

    private const float CapsuleCenterOffset = 0.9f;  // camera height - capsule half-height
    private const float NameplateHeadOffset = 1.1f;  // above capsule center → above head
    private const float InterpPeriodMs = 50f;
    private const float StaleFadeStartMs = 150f;     // alpha starts dropping here
    private const float StaleFadeEndMs = 500f;       // fully invisible + removed here
    private const int DefaultLayerMask = 1;          // layer 0 only

    private GUIStyle _nameStyle;

    private void Update()
    {
        var mgr = SessionManager.Current;
        if (mgr == null) return;
        long now = NowMs();
        // Destroy capsule + material for any entry about to be pruned so the scene
        // doesn't accumulate orphan capsules + leaked material instances on reconnect.
        foreach (var p in mgr.RemoteRegistry.All())
        {
            if (now - p.LastSeenMs > (long)StaleFadeEndMs)
                DestroyPlayerVisuals(p);
        }
        mgr.RemoteRegistry.PruneStale(nowMs: now, staleMs: (long)StaleFadeEndMs);
    }

    private void LateUpdate()
    {
        var mgr = SessionManager.Current;
        if (mgr == null) return;

        var cam = Camera.main;
        long now = NowMs();
        foreach (var p in mgr.RemoteRegistry.All())
        {
            EnsureCapsule(p);
            InterpolateTransform(p, now);
            ApplyLOSDim(p, cam, now);
        }
    }

    private void EnsureCapsule(RemotePlayer p)
    {
        if (p.Capsule != null) return;
        var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        go.name = "PCBSMultiplayer.RemoteCapsule.slot" + p.Slot;
        var col = go.GetComponent<Collider>();
        if (col != null) Destroy(col);
        var rend = go.GetComponent<Renderer>();
        var baseC = SlotColor.For(p.Slot);
        var mat = rend.material; // instance
        mat.color = new Color(baseC.r, baseC.g, baseC.b, 1f);
        p.Capsule = go;
        p.CapsuleMat = mat;
        Log.LogInfo("Spawned remote capsule for slot " + p.Slot + " (" + p.DisplayName + ")");
    }

    private void InterpolateTransform(RemotePlayer p, long now)
    {
        float t = Mathf.Clamp01((now - p.LastSampleMs) / InterpPeriodMs);
        var prev = new Vector3(p.PrevPosX, p.PrevPosY, p.PrevPosZ);
        var curr = new Vector3(p.PosX, p.PosY, p.PosZ);
        var pos = Vector3.Lerp(prev, curr, t);
        float yaw = Mathf.LerpAngle(p.PrevYaw, p.Yaw, t);
        if (p.Capsule is not GameObject go) return;
        go.transform.position = pos - Vector3.up * CapsuleCenterOffset;
        go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
    }

    private void ApplyLOSDim(RemotePlayer p, Camera cam, long now)
    {
        var mat = (Material)p.CapsuleMat;
        if (mat == null) return;
        var baseC = SlotColor.For(p.Slot);

        // LOS raycast.
        bool blocked = false;
        if (cam != null && p.Capsule is GameObject go)
        {
            Vector3 origin = cam.transform.position;
            Vector3 target = go.transform.position + Vector3.up * NameplateHeadOffset;
            Vector3 dir = target - origin;
            float dist = dir.magnitude;
            if (dist > 0.5f && Physics.Raycast(origin, dir.normalized, out var hit, dist, DefaultLayerMask))
            {
                blocked = hit.distance < dist - 0.1f;
            }
        }

        // Stale alpha fade.
        float staleMs = now - p.LastSeenMs;
        float alpha = 1f;
        if (staleMs > StaleFadeStartMs)
            alpha = Mathf.Clamp01(1f - (staleMs - StaleFadeStartMs) / (StaleFadeEndMs - StaleFadeStartMs));

        // LOS dim.
        var color = blocked
            ? new Color(0.5f, 0.5f, 0.5f, 0.6f * alpha)
            : new Color(baseC.r, baseC.g, baseC.b, alpha);
        mat.color = color;
    }

    private void OnGUI()
    {
        var mgr = SessionManager.Current;
        if (mgr == null) return;
        var cam = Camera.main;
        if (cam == null) return;

        if (_nameStyle == null)
        {
            _nameStyle = new GUIStyle(GUI.skin.label);
            _nameStyle.fontSize = 18;
            _nameStyle.fontStyle = FontStyle.Bold;
            _nameStyle.alignment = TextAnchor.MiddleCenter;
            _nameStyle.normal.textColor = Color.white;
        }

        long now = NowMs();
        foreach (var p in mgr.RemoteRegistry.All())
        {
            if (p.Capsule is not GameObject go) continue;

            Vector3 headWorld = go.transform.position + Vector3.up * NameplateHeadOffset;
            Vector3 screen = cam.WorldToScreenPoint(headWorld);
            if (screen.z <= 0f) continue;

            float x = screen.x;
            float y = Screen.height - screen.y;
            float avatarSize = 64f;

            // Stale alpha + LOS are already baked into capsule material; recompute for GUI.
            float staleMs = now - p.LastSeenMs;
            float alpha = staleMs > StaleFadeStartMs
                ? Mathf.Clamp01(1f - (staleMs - StaleFadeStartMs) / (StaleFadeEndMs - StaleFadeStartMs))
                : 1f;

            var prevColor = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, alpha);

            // Avatar (texture or initials placeholder).
            var tex = (p.Avatar as Texture2D) ?? SteamAvatarCache.TryGet(p.SteamId);
            if (tex != null) p.Avatar = tex;
            var avatarRect = new Rect(x - avatarSize / 2f, y - avatarSize - 24f, avatarSize, avatarSize);

            if (tex != null)
            {
                GUI.DrawTexture(avatarRect, tex);
            }
            else
            {
                // Gray placeholder + first two characters of DisplayName.
                var prev = GUI.color;
                GUI.color = new Color(0.3f, 0.3f, 0.3f, alpha);
                GUI.DrawTexture(avatarRect, Texture2D.whiteTexture);
                GUI.color = prev;
                string initials = (p.DisplayName ?? "").Length >= 2 ? p.DisplayName.Substring(0, 2).ToUpper()
                                : !string.IsNullOrEmpty(p.DisplayName) ? p.DisplayName.ToUpper()
                                : "??";
                GUI.Label(avatarRect, initials, _nameStyle);
            }

            // Name below avatar.
            var nameRect = new Rect(x - 100f, y - 20f, 200f, 22f);
            GUI.Label(nameRect, p.DisplayName ?? "", _nameStyle);

            GUI.color = prevColor;
        }
    }

    private void OnDestroy()
    {
        var mgr = SessionManager.Current;
        if (mgr == null) return;
        foreach (var p in mgr.RemoteRegistry.All())
            DestroyPlayerVisuals(p);
    }

    private static void DestroyPlayerVisuals(RemotePlayer p)
    {
        if (p.Capsule is GameObject go && go != null) Object.Destroy(go);
        if (p.CapsuleMat is Material mat && mat != null) Object.Destroy(mat);
        p.Capsule = null;
        p.CapsuleMat = null;
    }

    private static long NowMs() => (long)(Time.unscaledTime * 1000f);
}
