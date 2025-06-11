using UnityEngine;

public class CubeSpawner : MonoBehaviour
{
    public GameObject instance;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.P))
        {
            Vector3 spawn_pos = transform.position;
            Quaternion spawn_rot = transform.rotation;
            Instantiate(instance, spawn_pos, spawn_rot);
            Renderer renderer = instance.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = new Color(Random.value, Random.value, Random.value);
            }
            else
            {
                Debug.LogWarning("No Renderer found on the instance to change color.");
            }

        }
    }
}
