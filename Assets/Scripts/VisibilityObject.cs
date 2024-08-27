using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshRenderer))]
[ExecuteAlways]
public class VisibilityObject : MonoBehaviour
{
    private static List<VisibilityObject> objects = new List<VisibilityObject>();
    private static Dictionary<Mesh, int> meshes = new Dictionary<Mesh, int>();
    private static int objectMeshGlobalCount = 0;
    private static int objectMeshCountMax = int.MaxValue;

    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    public int meshGlobalStartIndex;
    public int subMeshCount;

    private void Awake()
    {
        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();
    }

    private void OnEnable()
    {
        objects.Add(this);
        subMeshCount = meshFilter.sharedMesh.subMeshCount;
        if (meshes.TryGetValue(meshFilter.sharedMesh, out int idx))
        {
            Debug.LogError($"{meshFilter.sharedMesh.name}已经被添加到VisibilityObject中!");
            meshGlobalStartIndex = idx;
        }
        else
        {
            meshGlobalStartIndex = objectMeshGlobalCount;
            objectMeshGlobalCount += subMeshCount;
            if (objectMeshGlobalCount > objectMeshCountMax)
            {
                Debug.LogError("VisibilityObject数量超过最大值!");
            }
        }
        MaterialPropertyBlock propertyBlock = new MaterialPropertyBlock();
        meshRenderer.GetPropertyBlock(propertyBlock);
        for (int i = 0; i < subMeshCount; i++)
        {
            propertyBlock.SetInt("_InstanceID", meshGlobalStartIndex + i);
            meshRenderer.SetPropertyBlock(propertyBlock, i);
        }
    }

    private void OnDisable()
    {
        objects.Remove(this);
    }
}
