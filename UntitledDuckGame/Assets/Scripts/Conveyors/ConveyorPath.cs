using System.Collections.Generic;
using UnityEngine;

public class ConveyorPath : MonoBehaviour {

    #region Segment Types
    public interface ISegment {
        float Length { get; }
        Vector3 GetPoint(float t);
        Vector3 GetTangent(float t);
    }

    public class StraightSegment : ISegment {
        readonly Vector3 a, b, dir;
        readonly float len;
        public StraightSegment(Vector3 a, Vector3 b) {
            this.a = a; this.b = b;
            dir = (b - a);
            len = dir.magnitude;
            if (len > 1e-5f) dir /= len;
        }
        public float Length => len;
        public Vector3 GetPoint(float t) => Vector3.Lerp(a, b, Mathf.Clamp01(t));
        public Vector3 GetTangent(float t) => dir;
    }
    #endregion

    [Header("Conveyor Path Settings")]
    [Tooltip("Auto-build the node list from ConveyorNodes found in children (hierarchy order). Drop segment prefabs under this object in travel order — straights carry 1 node, corners 2 — and the path assembles itself. Loop paths close back to the first node automatically. Off: the hand-wired list below is used as-is.")]
    [SerializeField] private bool autoCollectNodes = false;
    [Tooltip("Ignored when Auto Collect Nodes is on (the list is overwritten from children).")]
    [SerializeField] private List<ConveyorNode> nodes = new();
    [SerializeField] private int lineSubdiv = 8;
    [SerializeField] private int cornerSubdiv = 8;

    [Header("Belt Settings")]
    [Tooltip("Belt speed (units/sec) — carried items ride at this rate.")]
    [SerializeField] private float speed = 1f;
    [Tooltip("Closed loop: items circulate forever. Off: items fling off the end.")]
    [SerializeField] private bool loop = true;
    [Tooltip("Launch speed (velocity change) when a non-loop belt flings an item off its end.")]
    [SerializeField] private float exitForce = 3f;
    [Tooltip("Items with rigidbody mass above this can't ride this belt. 0 = no limit.")]
    [SerializeField] private float maxItemMass = 0f;
    [Tooltip("Physical width of the belt surface: items are captured and ride ANYWHERE across it, keeping their lane. 0 = line-only (per-item snapDistance).")]
    [SerializeField] private float beltWidth = 1f;

    // runtime registry of enabled belts, so free items can detect ANY belt they land
    // on (ConveyorObjectMover.FindCapturePath) without a pre-wired path reference
    private static readonly List<ConveyorPath> all = new();
    public static IReadOnlyList<ConveyorPath> All => all;

    #region Private Variables
    private List<Vector3> cornerPoints = new();
    private List<Vector3> cornerCenters = new();
    private readonly List<ISegment> segments = new();
    private float totalLength;
    #endregion

    #region Public Properties
    public float TotalLength => totalLength;
    public float MaxItemMass => maxItemMass;
    public float BeltWidth => beltWidth;
    public float Speed => speed;
    public bool Loop => loop;
    public float ExitForce => exitForce;
    #endregion

    #region Path Builder Functions
    //path builder
    private void Rebuild() {
        cornerPoints.Clear();
        cornerCenters.Clear();
        segments.Clear();
        totalLength = 0f;

        if (autoCollectNodes) {
            // hierarchy (depth-first) order = travel order: segments are ordered as
            // siblings, and each segment prefab keeps its own nodes in entry->exit order
            GetComponentsInChildren(nodes);
            // same closure convention as hand-wired loops: first node repeated at the
            // end, so the last->first pair becomes an ordinary segment
            if (loop && nodes.Count >= 2) nodes.Add(nodes[0]);
        }

        if (nodes == null || nodes.Count < 2)
            return;

        for (int i = 0; i < nodes.Count - 1; i++) {
            ConveyorNode nA = nodes[i];
            ConveyorNode nB = nodes[i + 1];

            Vector3 pA = nA.Position;
            Vector3 pB = nB.Position;

            //condition where the two nodes aren't at equal positions
            bool isProper = Mathf.Abs(Mathf.Abs(pA.x - pB.x) - Mathf.Abs(pA.z - pB.z)) < 1e-2f;

            if (nodes[i].turnDirection != 0f && isProper) {
                // Handle corner segments
                float direction = -Mathf.Sign(nA.turnDirection);
                float radius = Mathf.Abs(pA.x - pB.x);

                // forward vector of second node in corner 
                Vector3 pBf = nB.Forward;

                // * corner radius of first node
                Vector3 pBfScaled = pBf * radius;

                // add first node in corner to vector of second to find center
                Vector3 pCenter = pA + pBfScaled;

                if (nA.DebugOn) {
                    Debug.Log($"pA: {pA}, pBf: {pBf}, pBf Scaled: {pBfScaled}, pCenter: {pCenter}");
                }

                // for vis
                cornerCenters.Add(pCenter);
                //

                // derived from basic internal angle, but for one quadrant
                Vector3 offset = pA - pCenter;  // from center to first point

                float intAngleRad = Mathf.Atan2(offset.z, offset.x);
                float step = Mathf.PI / 2f / cornerSubdiv * direction;

                if (nA.DebugOn) {
                    Debug.Log($"internal angle: {intAngleRad} rad/{Mathf.Rad2Deg * intAngleRad}°, step: {step}");
                }


                Vector3 pU, pW;
                pU = pA;

                //iterate per corner segments
                for (int j = 0; j < cornerSubdiv; j++) {
                    intAngleRad += step;
                    // polar coords X and Y
                    float nX = pCenter.x + radius * Mathf.Cos(intAngleRad);
                    float nZ = pCenter.z + radius * Mathf.Sin(intAngleRad);

                    // location of next point
                    pW = new Vector3(nX, pA.y, nZ);


                    //vector from center to next point 
                    Vector3 pN = new Vector3(nX, pA.y, nZ);
                    Debug.DrawLine(pCenter, pN, Color.red);

                    // for vis
                    cornerPoints.Add(pN);
                    //

                    //add segment
                    var straight = new StraightSegment(pU, pW);
                    segments.Add(straight);
                    totalLength += straight.Length;

                    //intAngleRad += Mathf.PI / (cornerSubdiv * 2) * direction;
                    pU = pW;
                }


            }
            else {
                //straight segment
                var straight = new StraightSegment(pA, pB);
                segments.Add(straight);
                totalLength += straight.Length;
            }
        }
    }
    #endregion

    #region Path Helper Functions
    //path sampler 
    public (Vector3 pos, Vector3 tangent) SampleByDistance(float s) {
        s = Mathf.Clamp(s, 0, totalLength);
        float accum = 0f;
        foreach (var seg in segments) {
            if (s <= accum + seg.Length || seg == segments[^1]) {
                float t = (seg.Length > 1e-5f) ? (s - accum) / seg.Length : 0f;
                return (seg.GetPoint(t), seg.GetTangent(t));
            }
            accum += seg.Length;
        }
        //if overshot, return end of last segment
        var last = segments[^1];
        return (last.GetPoint(1f), last.GetTangent(1f));
    }

    // path sampler with smoothing
    public (Vector3 pos, Vector3 tangent) SampleByDistanceSmoothed(float s, float halfWindow) {
        var (pos, _) = SampleByDistance(s);

        if (totalLength <= 1e-4f) return (pos, Vector3.forward);

        // clamp window
        float ds = Mathf.Max(1e-3f, Mathf.Min(Mathf.Abs(halfWindow), totalLength * 0.05f));
        float s0 = Mathf.Clamp(s - ds, 0f, totalLength);
        float s1 = Mathf.Clamp(s + ds, 0f, totalLength);

        // safety check for collapsed ends
        if (Mathf.Approximately(s0, s1)) {
            ds = Mathf.Max(ds, totalLength * 0.005f + 1e-3f);
            s0 = Mathf.Clamp(s - ds, 0f, totalLength);
            s1 = Mathf.Clamp(s + ds, 0f, totalLength);
        }

        Vector3 p0 = PositionAtDistance(s0);
        Vector3 p1 = PositionAtDistance(s1);
        Vector3 tan = p1 - p0;

        if (tan.sqrMagnitude < 1e-8f) {
            tan = SampleByDistance(s).tangent;
        }

        return (pos, tan.normalized);
    }

    // return position along distance
    public Vector3 PositionAtDistance(float s) {
        s = Mathf.Clamp(s, 0f, totalLength);
        float accum = 0f;

        foreach (var seg in segments) {
            if (s <= accum + seg.Length || seg == segments[^1]) {
                float t = (seg.Length > 1e-5f) ? (s - accum) / seg.Length : 0f;
                return seg.GetPoint(t);
            }
            accum += seg.Length;
        }

        var last = segments[^1];
        return last.GetPoint(1f);
    }

    // Closest track distance s to a world position: coarse scan (>=64 samples, ~8/meter)
    // then a refined pass over a window around the best coarse hit. Used by movers for
    // capture and by segment visuals to derive scroll direction.
    public float FindClosestS(Vector3 worldPos) {
        if (totalLength <= 1e-4f || segments.Count == 0) return 0f;
        float L = totalLength;
        int N = Mathf.Max(64, Mathf.CeilToInt(L * 8f));
        float bestS = 0f;
        float bestD2 = float.PositiveInfinity;
        for (int i = 0; i <= N; i++) {
            float si = (L * i) / N;
            Vector3 pi = PositionAtDistance(si);
            float d2 = (pi - worldPos).sqrMagnitude;
            if (d2 < bestD2) { bestD2 = d2; bestS = si; }
        }
        float window = Mathf.Max(0.25f, L / N * 4f);
        int R = 24;
        for (int i = 0; i <= R; i++) {
            float si = Mathf.Clamp(bestS - window * 0.5f + window * (i / (float)R), 0f, L);
            Vector3 pi = PositionAtDistance(si);
            float d2 = (pi - worldPos).sqrMagnitude;
            if (d2 < bestD2) { bestD2 = d2; bestS = si; }
        }
        return bestS;
    }
    #endregion

    // Registry + rebuild run at runtime too (a build never fires the editor-only
    // callbacks, and the segment list must exist for movers to sample).
    #region Live Path Feedback
    private void OnEnable() {
        if (!all.Contains(this)) all.Add(this);
#if UNITY_EDITOR
        ConveyorNode.OnAnyNodeMoved += HandleNodeMovedOrChanged;
        ConveyorNode.OnDebugActivated += HandleNodeMovedOrChanged;
        ConveyorNode.OnDebugDeactivated += HandleNodeMovedOrChanged;
#endif
        Rebuild();
    }

    private void OnDisable() {
        all.Remove(this);
#if UNITY_EDITOR
        ConveyorNode.OnAnyNodeMoved -= HandleNodeMovedOrChanged;
        ConveyorNode.OnDebugActivated -= HandleNodeMovedOrChanged;
        ConveyorNode.OnDebugDeactivated -= HandleNodeMovedOrChanged;
#endif
    }

#if UNITY_EDITOR

    private void HandleNodeMovedOrChanged(ConveyorNode n) {
        // auto-collect: a just-added child node isn't in the list yet — IsChildOf
        // catches it so newly dropped segment prefabs rebuild the path immediately
        if ((nodes != null && nodes.Contains(n))
            || (autoCollectNodes && n != null && n.transform.IsChildOf(transform))) {
            Rebuild();
        }
    }
    private void OnValidate() { Rebuild(); }
    private void Reset() { Rebuild(); }

    private void OnDrawGizmos() {
        if (!Application.isPlaying) Rebuild();

        if (segments.Count == 0) Rebuild();
        Gizmos.color = Color.yellow;
        foreach (var seg in segments) {
            Vector3 prev = seg.GetPoint(0f);
            for (int i = 1; i <= lineSubdiv; i++) {
                float t = i / (float)lineSubdiv;
                Vector3 p = seg.GetPoint(t);
                Gizmos.DrawLine(prev, p);
                prev = p;
            }
        }

        // belt width visualization: cyan edge lines at ±width/2 along the whole path,
        // showing the actual riding surface (capture zone + lanes), not just the spine
        if (beltWidth > 0f && totalLength > 1e-4f) {
            Gizmos.color = Color.cyan;
            float half = beltWidth * 0.5f;
            int steps = Mathf.Max(8, Mathf.CeilToInt(totalLength * 4f));
            Vector3 prevL = Vector3.zero, prevR = Vector3.zero;
            bool hasPrev = false;
            for (int i = 0; i <= steps; i++) {
                float dist = totalLength * i / steps;
                var (p, tan) = SampleByDistanceSmoothed(dist, 0.25f);
                if (tan.sqrMagnitude < 1e-8f) continue;
                Vector3 right = Vector3.Cross(Vector3.up, tan).normalized;
                Vector3 l = p - right * half;
                Vector3 r = p + right * half;
                if (hasPrev) {
                    Gizmos.DrawLine(prevL, l);
                    Gizmos.DrawLine(prevR, r);
                }
                prevL = l;
                prevR = r;
                hasPrev = true;
            }
        }

        for (int i = 0; i < cornerPoints.Count; i++) {
            // draw centers big
            Gizmos.color = Color.black;
            foreach (var c in cornerCenters)
                Gizmos.DrawSphere(c, 0.075f);

            // draw arc points small
            Gizmos.color = Color.green;
            foreach (var p in cornerPoints)
                Gizmos.DrawSphere(p, 0.05f);
        }
    }
#endif
    #endregion
}
