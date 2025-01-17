using System.Collections.Generic;
using UnityEngine;
using static VisibilityObject;

public class VisibilityBufferRenderingMgr : MonoBehaviour
{
    public static VisibilityBufferRenderingMgr Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<VisibilityBufferRenderingMgr>();
                if (instance == null)
                {
                    GameObject go = new GameObject("VisibilityBufferRenderingMgr");
                    instance = go.AddComponent<VisibilityBufferRenderingMgr>();
                }
            }
            return instance;
        }
    }
    private static VisibilityBufferRenderingMgr instance;

    public List<VisibilityObject> objects = new List<VisibilityObject>();

    //Key:Mesh, Value:SubMeshStartIndex
    private Dictionary<Mesh, int[]> meshes = new Dictionary<Mesh, int[]>();

    //Key:Material, Value:MaterialIndex
    private Dictionary<Material, int> materials = new Dictionary<Material, int>();

    private List<float> vertexData = new List<float>();//position, normal, tangent, uv
    private int vertexCount = 0;
    private List<int> indexData = new List<int>();//index
    private List<float> objectToWorldMatrices = new List<float>();

    private int GetOrAddMaterialIdx(Material material)
    {
        if (materials.TryGetValue(material, out int materialIdx))
        {
            return materialIdx;
        }
        materials.Add(material, materials.Count);
        return materials.Count - 1;
    }

    public VisibilityObjectData ResigterObject(VisibilityObject obj)
    {
        VisibilityObjectData data = new VisibilityObjectData();

        var mesh = obj.meshFilter.sharedMesh;
        data.subMeshData = new List<VisibilityObjectSubMeshData>(mesh.subMeshCount);
        if (meshes.TryGetValue(mesh, out int[] subMeshStartIndex))
        {
            for (int i = 0; i < subMeshStartIndex.Length; i++)
            {
                VisibilityObjectSubMeshData subMeshData = new VisibilityObjectSubMeshData();
                subMeshData.materialID = GetOrAddMaterialIdx(obj.meshRenderer.sharedMaterials[i]);
                subMeshData.subMeshStartIndex = subMeshStartIndex[i];
                data.subMeshData.Add(subMeshData);
            }
        }
        else
        {
            for (int i = 0; i < mesh.vertexCount; i++)
            {
                //position
                vertexData.Add(mesh.vertices[i].x);
                vertexData.Add(mesh.vertices[i].y);
                vertexData.Add(mesh.vertices[i].z);
                //normal
                vertexData.Add(mesh.normals[i].x);
                vertexData.Add(mesh.normals[i].y);
                vertexData.Add(mesh.normals[i].z);
                //tangent
                vertexData.Add(mesh.tangents[i].x);
                vertexData.Add(mesh.tangents[i].y);
                vertexData.Add(mesh.tangents[i].z);
                vertexData.Add(mesh.tangents[i].w);
                //uv0
                vertexData.Add(mesh.uv[i].x);
                vertexData.Add(mesh.uv[i].y);
            }

            int subMeshCount = obj.meshFilter.sharedMesh.subMeshCount;
            int[] subMeshStartIndexNew = new int[subMeshCount];
            for (int i = 0; i < subMeshCount; i++)
            {
                int[] indices = mesh.GetIndices(i);
                for (int j = 0; j < indices.Length; j++)
                {
                    indices[j] += vertexCount;
                }
                subMeshStartIndexNew[i] = indexData.Count;
                indexData.AddRange(indices);

                VisibilityObjectSubMeshData subMeshData = new VisibilityObjectSubMeshData();
                subMeshData.materialID = GetOrAddMaterialIdx(obj.meshRenderer.sharedMaterials[i]);
                subMeshData.subMeshStartIndex = subMeshStartIndexNew[i];
                data.subMeshData.Add(subMeshData);
            }
            vertexCount += mesh.vertexCount;
            meshes.Add(mesh, subMeshStartIndexNew);
        }

        data.instanceID = objectToWorldMatrices.Count;
        objectToWorldMatrices.Add(obj.transform.localToWorldMatrix.m00);
        objectToWorldMatrices.Add(obj.transform.localToWorldMatrix.m01);
        objectToWorldMatrices.Add(obj.transform.localToWorldMatrix.m02);
        objectToWorldMatrices.Add(obj.transform.localToWorldMatrix.m03);
        objectToWorldMatrices.Add(obj.transform.localToWorldMatrix.m10);
        objectToWorldMatrices.Add(obj.transform.localToWorldMatrix.m11);
        objectToWorldMatrices.Add(obj.transform.localToWorldMatrix.m12);
        objectToWorldMatrices.Add(obj.transform.localToWorldMatrix.m13);
        objectToWorldMatrices.Add(obj.transform.localToWorldMatrix.m20);
        objectToWorldMatrices.Add(obj.transform.localToWorldMatrix.m21);
        objectToWorldMatrices.Add(obj.transform.localToWorldMatrix.m22);
        objectToWorldMatrices.Add(obj.transform.localToWorldMatrix.m23);
        objectToWorldMatrices.Add(obj.transform.localToWorldMatrix.m30);
        objectToWorldMatrices.Add(obj.transform.localToWorldMatrix.m31);
        objectToWorldMatrices.Add(obj.transform.localToWorldMatrix.m32);
        objectToWorldMatrices.Add(obj.transform.localToWorldMatrix.m33);
        objects.Add(obj);

        return data;
    }

    public void RemoveObject(VisibilityObject obj)
    {
        objects.Remove(obj);
    }

    public void CreateBufferIfNeed(ref ComputeBuffer vertexBuffer, ref ComputeBuffer indexBuffer, ref ComputeBuffer objectToWorldMatrixBuffer)
    {
        if (vertexBuffer == null)
        {
            vertexBuffer = new ComputeBuffer(vertexData.Count, sizeof(float));
            vertexBuffer.SetData(vertexData);
        }
        if (indexBuffer == null)
        {
            indexBuffer = new ComputeBuffer(indexData.Count, sizeof(int));
            indexBuffer.SetData(indexData);
        }
        if (objectToWorldMatrixBuffer == null)
        {
            objectToWorldMatrixBuffer = new ComputeBuffer(objectToWorldMatrices.Count, sizeof(float));
            objectToWorldMatrixBuffer.SetData(objectToWorldMatrices);
        }
    }
}
