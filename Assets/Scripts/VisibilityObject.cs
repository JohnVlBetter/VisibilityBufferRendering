using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshRenderer))]
[ExecuteAlways]
public class VisibilityObject : MonoBehaviour
{
    public static List<VisibilityObject> objects = new List<VisibilityObject>();
    public MeshRenderer meshRenderer;

    private void Awake()
    {
        meshRenderer = GetComponent<MeshRenderer>();
    }

    private void OnEnable()
    {
        objects.Add(this);
    }

    private void OnDisable()
    {
        objects.Remove(this);
    }
}
