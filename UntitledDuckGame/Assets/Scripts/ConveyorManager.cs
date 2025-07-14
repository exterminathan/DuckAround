using System.Linq;
using UnityEngine;

public class ConveyorManager : MonoBehaviour {
    public MeshRenderer[] conveyorBeltRenderers;

    [SerializeField] private float scrollSpeed = 0.5f;

    [Header("Initial tiling and offset")]
    [SerializeField] private Vector2 initTiling = new Vector2(2.5f, 1f);
    [SerializeField] private Vector2 initOffset = new Vector2(-0.053f, 0f);

    private Vector2 currOffset;

    private void Start() {
        currOffset = initOffset;

        foreach (var rend in conveyorBeltRenderers) {
            var mat = rend.material;
            mat.SetVector("_BaseMap_ST", new Vector4(initTiling.x, initTiling.y, currOffset.x, currOffset.y));
        }
    }

    private void Update() {
        currOffset.y += scrollSpeed * Time.deltaTime;

        foreach (var rend in conveyorBeltRenderers) {
            var mat = rend.material;
            mat.SetVector("_BaseMap_ST", new Vector4(initTiling.x, initTiling.y, currOffset.x, currOffset.y));
        }
    }
}
