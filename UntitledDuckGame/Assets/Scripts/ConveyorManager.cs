using System.Linq;
using UnityEngine;

public class ConveyorManager : MonoBehaviour {
    public MeshRenderer[] conveyorBeltRenderers;
    public MeshRenderer[] cornerBeltRenderers;

    [SerializeField] private float scrollSpeed = 0.5f;

    [Header("Regular Tiling + Offset")]
    [SerializeField] private Vector2 regInitTiling = new Vector2(.5f, 1f);
    [SerializeField] private Vector2 regInitOffset = new Vector2(-0.053f, 0f);

    [Header("Corner Tiling + Offset")]
    [SerializeField] private Vector2 crnInitTiling = new Vector2(1f, 1f);
    [SerializeField] private Vector2 crnInitOffset = new Vector2(-0.006f, -0.43f);

    private Vector2 regCurrOffset;
    private Vector2 crnCurrOffset;

    private void Start() {
        regCurrOffset = regInitOffset;

        foreach (var rend in conveyorBeltRenderers) {
            var mat = rend.material;
            mat.SetVector("_BaseMap_ST", new Vector4(regInitTiling.x, regInitTiling.y, regCurrOffset.x, regCurrOffset.y));
        }

        crnCurrOffset = crnInitOffset;

        foreach (var rend in cornerBeltRenderers) {
            var mat = rend.material;
            mat.SetVector("_BaseMap_ST", new Vector4(crnInitTiling.x, crnInitTiling.y, crnCurrOffset.x, crnCurrOffset.y));
        }
    }

    private void Update() {
        regCurrOffset.y += scrollSpeed * Time.deltaTime;
        crnCurrOffset.y += scrollSpeed * Time.deltaTime;

        foreach (var rend in conveyorBeltRenderers) {
            var mat = rend.material;
            mat.SetVector("_BaseMap_ST", new Vector4(regInitTiling.x, regInitTiling.y, regCurrOffset.x, regCurrOffset.y));
        }

        foreach (var rend in cornerBeltRenderers) {
            var mat = rend.material;
            mat.SetVector("_BaseMap_ST", new Vector4(crnInitTiling.x, crnInitTiling.y, crnCurrOffset.x, crnCurrOffset.y));
        }
    }
}
