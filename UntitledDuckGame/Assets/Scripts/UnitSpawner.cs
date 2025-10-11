using UnityEngine;

public class UnitSpawner : MonoBehaviour {
    public Transform[] units;
    public ConveyorPath cPath;


    // Update is called once per frame
    void Update() {
        if (Input.GetKeyDown(KeyCode.Tab)) {
            SpawnUnit();
        }
    }

    private void SpawnUnit() {
        if (units != null) {
            var obj = Instantiate(units[Random.Range(0, units.Length)], transform.position, transform.rotation);
            obj.GetComponent<ConveyorObjectMover>().path = cPath;
        }
        else {
            Debug.LogWarning("Unit prefab is not assigned.");
        }
    }
}
