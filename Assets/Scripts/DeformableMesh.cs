using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class DeformableMesh : MonoBehaviour
{
    [SerializeField] private Vector2Int _vertexRange = new Vector2Int(5, 5);
    [SerializeField] private float _vertexSpacing = 1.0f;
    [SerializeField] private Material _material;
    
    private MeshFilter _meshFilter;
    private MeshRenderer _meshRenderer;
    
    private List<Vector3> _vertices = new();

    private void Awake()
    {
        Debug.Assert(_material != null, "No material assigned.");
        _meshFilter = GetComponent<MeshFilter>();
        _meshRenderer = GetComponent<MeshRenderer>();
    }
    
    private void OnGUI()
    {
        GenerateMesh();
    }

    private void GenerateMesh()
    {
        var mesh = new Mesh();

        _vertices = ComputeVertices();
        int[] triangles = ComputeTriangles();

        mesh.SetVertices(_vertices);
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
        _meshFilter.mesh = mesh;
        _meshRenderer.material = _material;
    }

    private List<Vector3> ComputeVertices()
    {
        var result = new List<Vector3>();
        for (int i = 0; i < _vertexRange.y; i++)
        {
            for (int j = 0; j < _vertexRange.x; j++)
            {
                result.Add(new Vector3(j * _vertexSpacing, i * _vertexSpacing, 0));
            }
        }

        return result;
    }

    private int[] ComputeTriangles()
    {
        int[] triangles = new int[_vertexRange.x * _vertexRange.y * 6];
        int triangleIndex = 0;
        
        for (int i = 0; i < _vertexRange.y - 1; i++)
        {
            for (int j = 0; j < _vertexRange.x - 1; j++)
            {
                int topLeft = j + (i * _vertexRange.x);
                int topRight = topLeft + 1;
                int botLeft = topLeft + _vertexRange.x;
                int botRight = botLeft + 1;
                triangles[triangleIndex++] = botLeft;
                triangles[triangleIndex++] = topLeft;
                triangles[triangleIndex++] = topRight;
                triangles[triangleIndex++] = topRight;
                triangles[triangleIndex++] = botRight;
                triangles[triangleIndex++] = botLeft;
            }
        }

        return triangles;
    }
}
