using UnityEngine;

public class UnitSpawner : MonoBehaviour {
    public Transform unit;
    public ConveyorPath cPath;


    // Update is called once per frame
    void Update() {
        if (Input.GetKeyDown(KeyCode.Tab)) {
            SpawnUnit();
        }
    }

    private void SpawnUnit() {
        if (unit != null) {
            var obj = Instantiate(unit, transform.position, transform.rotation);
            obj.GetComponent<ConveyorObjectMover>().path = cPath;
        }
        else {
            Debug.LogWarning("Unit prefab is not assigned.");
        }
    }
}
