using System.Collections.Generic;
using UnityEngine;

public class ConveyorPath : MonoBehaviour {

    [SerializeField] private List<ConveyorNode> nodes = new();
    [SerializeField] private int lineSubdiv = 8;

    // Path segment types
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


    private readonly List<ISegment> segments = new();
    private float totalLength;
    public float TotalLength => totalLength;


    //path builder
    private void Rebuild() {
        segments.Clear();
        totalLength = 0f;
        if (nodes == null || nodes.Count < 2) return;

        // straight segments between all nodes
        for (int i = 0; i < nodes.Count - 1; i++) {
            Vector3 pA = nodes[i].Position;
            Vector3 pB = nodes[i + 1].Position;
            var straight = new StraightSegment(pA, pB);
            segments.Add(straight);
            totalLength += straight.Length;
        }
    }

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


#if UNITY_EDITOR
    private void OnValidate() { Rebuild(); }
    private void Reset() { Rebuild(); }

    private void OnDrawGizmos() {
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
    }
#endif
}
