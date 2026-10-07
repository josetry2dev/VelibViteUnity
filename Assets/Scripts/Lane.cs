using UnityEngine;
using UnityEngine.Splines;

[RequireComponent(typeof(SplineContainer))]
public class Lane : MonoBehaviour {
    public IntersectionNode nextIntersection;
    public SplineContainer GetSplineContainer() => GetComponent<SplineContainer>();
}