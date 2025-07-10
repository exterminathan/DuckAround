// using System.Collections.Generic;
// using UnityEngine;
// using static BehaviourTreeNodes;

// public abstract class BehaviourTreeRunner : MonoBehaviour {
//     protected Node rootNode;
//     protected Dictionary<string, object> state;

//     protected abstract void InitializeState();
//     protected abstract Node BuildTree();
//     protected virtual void UpdateState() {
//         state["SelfTransform"] = transform;
//     }

//     private void Start() {
//         state = new Dictionary<string, object>();
//         InitializeState();
//         rootNode = BuildTree();
//     }

//     private void Update() {
//         UpdateState();
//         rootNode?.Execute(state);
//     }
// }
