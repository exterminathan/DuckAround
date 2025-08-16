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
    [SerializeField] private List<ConveyorNode> nodes = new();
    [SerializeField] private int lineSubdiv = 8;
    [SerializeField] private int cornerSubdiv = 8;

    #region Private Variables
    private List<Vector3> cornerPoints = new();
    private List<Vector3> cornerCenters = new();
    private readonly List<ISegment> segments = new();
    private float totalLength;
    #endregion

    #region Public Properties
    public float TotalLength => totalLength;
    #endregion

    #region Path Builder Functions
    //path builder
    private void Rebuild() {
        cornerPoints.Clear();
        cornerCenters.Clear();
        segments.Clear();
        totalLength = 0f;

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
    #endregion

    // Live Feedback System In-Editor for Nodes & Paths
    #region Live Path Feedback
#if UNITY_EDITOR
    private void OnEnable() {
        ConveyorNode.OnAnyNodeMoved += HandleNodeMovedOrChanged;
        ConveyorNode.OnDebugActivated += HandleNodeMovedOrChanged;
        ConveyorNode.OnDebugDeactivated += HandleNodeMovedOrChanged;
        Rebuild();
    }

    private void OnDisable() {
        ConveyorNode.OnAnyNodeMoved -= HandleNodeMovedOrChanged;
        ConveyorNode.OnDebugActivated -= HandleNodeMovedOrChanged;
        ConveyorNode.OnDebugDeactivated -= HandleNodeMovedOrChanged;
    }

    private void HandleNodeMovedOrChanged(ConveyorNode n) {
        if (nodes != null && nodes.Contains(n)) {
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
