using UnityEngine;

public class ConveyorManager : MonoBehaviour
{
    [Header("Assign the shared conveyor material here")]
    [SerializeField] private Material conveyorBeltMaterial;

    [Header("Scroll speed in UV units per second")]
    [SerializeField] private float scrollSpeed = 0.5f;

    [Header("Initial tiling and offset (in UV space)")]
    [SerializeField] private Vector2 initTiling = new Vector2(0.5f, 1f);
    [SerializeField] private Vector2 initOffset = new Vector2(-0.053f, 0f);

    private Vector2 currOffset;

    private void Start()
    {
        if (conveyorBeltMaterial == null)
        {
            Debug.LogError("ConveyorManager: No material assigned!", this);
            enabled = false;
            return;
        }

        // Make sure the texture repeats rather than clamps
        conveyorBeltMaterial.mainTexture.wrapMode = TextureWrapMode.Repeat;

        // Apply your initial tiling and offset
        conveyorBeltMaterial.SetTextureScale("_MainTex", initTiling);
        currOffset = initOffset;
        conveyorBeltMaterial.SetTextureOffset("_MainTex", currOffset);
    }

    private void Update()
    {
        // Advance the offset and wrap it between 0–1
        currOffset.x = (currOffset.x + scrollSpeed * Time.deltaTime) % 1f;
        conveyorBeltMaterial.SetTextureOffset("_MainTex", currOffset);
    }
}
