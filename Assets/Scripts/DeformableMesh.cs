using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace MeshToy
{
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshCollider))]
    [RequireComponent(typeof(MeshRenderer))]
    public class DeformableMesh : MonoBehaviour
    {
        [FormerlySerializedAs("_controlArgs")] [SerializeField]
        private DeformableMeshDataModel _dataModel = new()
        {
            VertexRange = new Vector2Int(5, 5),
            VertexSpacing = 1.0f,
            DeformSpeed = 1.0f
        };

        private DeformableMeshDependencies _deformableMeshDependencies;
        private Vector3 _origin;
        private List<Vector3> _vertices = new();
        private List<int> _brushedVertices = new();
        private Mesh _mesh;

        #region Unity Methods

        private void Awake()
        {
            Debug.Assert(_dataModel.Material != null, "No material assigned.");
            _deformableMeshDependencies.MeshFilter = GetComponent<MeshFilter>();
            _deformableMeshDependencies.MeshCollider = GetComponent<MeshCollider>();
            _deformableMeshDependencies.MeshRenderer = GetComponent<MeshRenderer>();
            _deformableMeshDependencies.MainCamera = Camera.main;
        }

        private void Start()
        {
            GenerateMesh();
        }

        private void OnDrawGizmos()
        {
            foreach (Vector3 vert in _vertices)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawSphere(vert, 0.01f);
            }
        }

        private void OnGUI()
        {
            if (_brushedVertices.Count > 0)
            {
                InvalidateMesh();
            }
        }

        private void Update()
        {
            HandleMouseInput();
        }

        #endregion

        private void HandleMouseInput()
        {
            _brushedVertices.Clear();
            if (!Input.GetMouseButton(0)) { return; }

            Vector3 mousePos = Input.mousePosition;
            Ray ray = _deformableMeshDependencies.MainCamera.ScreenPointToRay(mousePos);

            if (!Physics.SphereCast(ray, _dataModel.BrushRadius, out RaycastHit hitInfo, Mathf.Infinity, _dataModel.HitLayerMask))
            {
                return;
            }

            float deformDelta = _dataModel.DeformSpeed * Time.deltaTime;
            Vector3 hitPoint = hitInfo.point;
            _brushedVertices = GetVerticesInRadius(hitPoint, _dataModel.BrushRadius);
            foreach (int index in _brushedVertices)
            {
                _vertices[index] += -Vector3.forward * deformDelta;
            }

            
        }

        private void InvalidateMesh()
        {
            _mesh.SetVertices(_vertices);
            _mesh.RecalculateBounds();
            _mesh.RecalculateNormals();
        }

        #region Private Methods
        
        private List<int> GetVerticesInRadius(Vector3 hitPoint, float brushRadius)
        {
            List<int> brushedVertices = new();

            for (int i = 0; i < _vertices.Count; i++)
            {
                Vector3 worldVert = transform.TransformPoint(_vertices[i]);
                if (Vector3.Distance(worldVert, hitPoint) <= brushRadius) { brushedVertices.Add(i); }
            }

            return brushedVertices;
        }

        private void GenerateMesh()
        {
            Debug.Log("Generating mesh...");
            _origin = CalculateMeshOrigin();
            _mesh = new Mesh();

            _vertices = ComputeVertices();
            int[] triangles = ComputeTriangles();

            _mesh.SetVertices(_vertices);
            _mesh.triangles = triangles;
            _mesh.RecalculateBounds();
            _deformableMeshDependencies.MeshFilter.mesh = _mesh;
            _deformableMeshDependencies.MeshCollider.sharedMesh = _mesh;
            _deformableMeshDependencies.MeshRenderer.material = _dataModel.Material;
        }

        private Vector3 CalculateMeshOrigin()
        {
            Vector2 range = (Vector2)_dataModel.VertexRange * _dataModel.VertexSpacing;
            var offset = new Vector3(
                (range.x - 1) * 0.5f,
                (range.y - 1) * 0.5f,
                0);

            return transform.InverseTransformPoint(transform.position - offset);
        }

        private List<Vector3> ComputeVertices()
        {
            Vector2Int range = _dataModel.VertexRange;
            float spacing = _dataModel.VertexSpacing;
            var result = new List<Vector3>();
            for (int i = 0; i < range.y; i++)
            for (int j = 0; j < range.x; j++)
                result.Add(_origin + new Vector3(j * spacing, i * spacing, 0));

            return result;
        }

        private int[] ComputeTriangles()
        {
            Vector2Int range = _dataModel.VertexRange;
            int[] triangles = new int[range.x * range.y * 6];
            int triangleIndex = 0;

            for (int i = 0; i < range.y - 1; i++)
            for (int j = 0; j < range.x - 1; j++)
            {
                int topLeft = j + i * range.x;
                int topRight = topLeft + 1;
                int botLeft = topLeft + range.x;
                int botRight = botLeft + 1;
                triangles[triangleIndex++] = botLeft;
                triangles[triangleIndex++] = topLeft;
                triangles[triangleIndex++] = topRight;
                triangles[triangleIndex++] = topRight;
                triangles[triangleIndex++] = botRight;
                triangles[triangleIndex++] = botLeft;
            }

            return triangles;
        }
        
        #endregion
    }
}