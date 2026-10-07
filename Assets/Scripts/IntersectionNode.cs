using System.Collections.Generic;
using UnityEngine;

public class IntersectionNode : MonoBehaviour {
    [System.Serializable]
    public class Link { public Lane input; public List<Lane> outputs; }
    public List<Link> links;

    public Lane GetNextLane(Lane from) {
        var link = links.Find(l => l.input == from);
        return (link != null && link.outputs.Count > 0) ? link.outputs[Random.Range(0, link.outputs.Count)] : null;
    }
}