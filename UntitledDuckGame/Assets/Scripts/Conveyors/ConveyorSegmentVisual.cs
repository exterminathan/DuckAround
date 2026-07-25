using UnityEngine;

// Per-segment belt visual: scrolls this segment's _BaseMap V offset at the parent
// ConveyorPath's Speed, replacing the old global ConveyorManager. Tiling and base
// offset are READ from the material asset at startup (already baked into
// conveyor_belt.mat / corner_belt.mat), and the scroll sign is auto-derived from
// the path tangent at this segment's position — so a placed segment needs zero
// wiring beyond sitting under a ConveyorPath. Runtime-only: no edit-mode preview,
// so shared material assets are never touched.
[DisallowMultipleComponent]
public class ConveyorSegmentVisual : MonoBehaviour {

    public enum ScrollDirection { Auto, Forward, Reverse }

    private static readonly int BaseMapStId = Shader.PropertyToID("_BaseMap_ST");
    // |dot(tangent, travel axis)| below this is effectively perpendicular — the auto
    // sign would be a coin flip, so warn and fall back to Forward
    private const float ambiguousDot = 0.2f;

    [Header("Belt Visual Settings")]
    [Tooltip("Renderer whose belt material scrolls. Assign the belt MeshRenderer inside the nested FBX.")]
    [SerializeField] private MeshRenderer beltRenderer;
    [Tooltip("UV offset advanced per meter of belt travel. Calibrate per prefab so the texture keeps pace with riding items (straight ≈ 1).")]
    [SerializeField] private float uvPerMeter = 1f;
    [Tooltip("Local direction the belt texture VISUALLY moves when scrolling Forward. Auto dots this against the path tangent to pick the sign.")]
    [SerializeField] private Vector3 localTravelAxis = Vector3.forward;
    [Tooltip("Auto: derive scroll sign from the parent path's travel direction here. Forward/Reverse: manual override for ambiguous placements (hairpins, parallel runs).")]
    [SerializeField] private ScrollDirection scrollDirection = ScrollDirection.Auto;

    private ConveyorPath path;
    private Material beltMaterial; // per-renderer instance; created in Start, destroyed in OnDestroy
    private Vector4 baseST;        // tiling (x,y) + baked base offset (z,w) from the material asset
    private float sign = 1f;
    private float scrollV;         // accumulated V scroll, wrapped to [0,1)

    private void Start() {
        if (beltRenderer == null) {
            Debug.LogWarning($"[{name}] ConveyorSegmentVisual: no belt renderer assigned.", this);
            enabled = false;
            return;
        }

        // no parent ConveyorPath = static decor segment: keep the baked material
        // look, never scroll, never instance a material
        path = GetComponentInParent<ConveyorPath>();
        if (path == null) {
            enabled = false;
            return;
        }

        // .material instances this renderer's material so the shared .mat asset is
        // never mutated (and each segment keeps its own scroll phase)
        beltMaterial = beltRenderer.material;
        baseST = beltMaterial.GetVector(BaseMapStId);

        ResolveSign();
    }

    private void Update() {
        if (path == null) return; // path destroyed mid-play

        // offset adds post-tiling in the shader, so its period is exactly 1
        // regardless of tiling — Repeat keeps scrollV drift-free forever
        scrollV = Mathf.Repeat(scrollV + sign * path.Speed * uvPerMeter * Time.deltaTime, 1f);
        beltMaterial.SetVector(BaseMapStId, new Vector4(baseST.x, baseST.y, baseST.z, baseST.w + scrollV));
    }

    private void OnDestroy() {
        if (beltMaterial != null) Destroy(beltMaterial);
    }

    private void ResolveSign() {
        if (scrollDirection == ScrollDirection.Forward) { sign = 1f; return; }
        if (scrollDirection == ScrollDirection.Reverse) { sign = -1f; return; }

        if (path.TotalLength <= 1e-4f) { sign = 1f; return; }

        float s = path.FindClosestS(transform.position);
        Vector3 tangent = path.SampleByDistance(s).tangent;
        Vector3 travel = transform.TransformDirection(localTravelAxis).normalized;
        float d = Vector3.Dot(tangent, travel);

        if (Mathf.Abs(d) < ambiguousDot) {
            Debug.LogWarning($"[{name}] Belt scroll sign ambiguous (|dot|={Mathf.Abs(d):F2}) — check localTravelAxis or set a manual override.", this);
            sign = 1f;
            return;
        }
        sign = Mathf.Sign(d);
    }
}
