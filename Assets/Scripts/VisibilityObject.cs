using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshRenderer))]
public class VisibilityObject : MonoBehaviour
{
    private static List<VisibilityObject> objects = new List<VisibilityObject>();
    private static Dictionary<Mesh, int> meshes = new Dictionary<Mesh, int>();
    private static Dictionary<Material, int> materials = new Dictionary<Material, int>();
    private static int objectMeshGlobalCount = 0;
    private static int objectMaterialGlobalCount = 0;
    private static int objectCountMax = int.MaxValue;

    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;

    public int meshGlobalStartIndex;
    public int materialGlobalStartIndex;
    private int subMeshCount;

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
            Debug.LogError($"Mesh:{meshFilter.sharedMesh.name}已经被添加!");
            meshGlobalStartIndex = idx;
        }
        else
        {
            meshGlobalStartIndex = objectMeshGlobalCount;
            objectMeshGlobalCount += subMeshCount;
            meshes.Add(meshFilter.sharedMesh, meshGlobalStartIndex);
            if (objectMeshGlobalCount > objectCountMax)
            {
                Debug.LogError("Visibility Mesh数量超过最大值!");
            }
        }

        MaterialPropertyBlock propertyBlock = new MaterialPropertyBlock();
        meshRenderer.GetPropertyBlock(propertyBlock);
        for (int i = 0; i < subMeshCount; i++)
        {
            var mat = meshRenderer.sharedMaterials[i];
            if (materials.TryGetValue(mat, out idx))
            {
                Debug.LogError($"Material:{mat.name}已经被添加!");
            }
            else
            {
                materialGlobalStartIndex = objectMaterialGlobalCount++;
                materials.Add(mat, materialGlobalStartIndex);
                propertyBlock.SetInt("_InstanceID", meshGlobalStartIndex + i);
                propertyBlock.SetInt("_MaterialID", materialGlobalStartIndex);
                meshRenderer.SetPropertyBlock(propertyBlock, i);
                if (objectMaterialGlobalCount > objectCountMax)
                {
                    Debug.LogError("Visibility Material数量超过最大值!");
                }
            }
        }
    }

    private void OnDisable()
    {
        objects.Remove(this);
    }
}
