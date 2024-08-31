using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshRenderer))]
//[ExecuteAlways]
public class VisibilityObject : MonoBehaviour
{
    public struct VisibilityObjectSubMeshData
    {
        public int materialID;
        public int subMeshStartIndex;
    }

    public class VisibilityObjectData
    {
        public int instanceID;
        public List<VisibilityObjectSubMeshData> subMeshData;
    }

    public MeshFilter meshFilter;
    public MeshRenderer meshRenderer;
    public VisibilityObjectData visibilityObjectData;

    private void Awake()
    {
        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();
    }

    private void OnEnable()
    {
        visibilityObjectData = VisibilityBufferRenderingMgr.Instance.ResigterObject(this);
        MaterialPropertyBlock materialPropertyBlock = new MaterialPropertyBlock();
        for (int i = 0; i < meshRenderer.sharedMaterials.Length; i++)
        {
            meshRenderer.GetPropertyBlock(materialPropertyBlock, i);
            VisibilityObjectSubMeshData subMeshData = visibilityObjectData.subMeshData[i];
            materialPropertyBlock.SetInt("_InstanceID", visibilityObjectData.instanceID);
            materialPropertyBlock.SetInt("_MaterialID", subMeshData.materialID);
            materialPropertyBlock.SetInt("_SubMeshStartIndex", subMeshData.subMeshStartIndex);
            meshRenderer.SetPropertyBlock(materialPropertyBlock, i);
        }
    }

    private void OnDisable()
    {
        VisibilityBufferRenderingMgr.Instance.RemoveObject(this);
    }
}