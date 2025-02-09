using System.Collections.Generic;
using UnityEngine;
using Color = UnityEngine.Color;

namespace MeshToy
{
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshCollider))]
    [RequireComponent(typeof(MeshRenderer))]
    public class DeformableMesh : MonoBehaviour
    {
        private static readonly int Property = Shader.PropertyToID("_MaxDepth");

        [SerializeField] private DeformableMeshDataModel _dataModel = new()
        {
            VertexRange = new Vector2Int(5, 5),
            VertexSpacing = 1.0f,
            DeformSpeed = 1.0f,
            HealingRate = 1.0f,
            MaxDepth = 1.0f
        };

        private bool LeftMouseDownExclusive => _leftMouseButtonDown && !_rightMouseButtonDown;
        private bool RightMouseDownExclusive => _rightMouseButtonDown && !_leftMouseButtonDown;

        private DeformableMeshDependencies _deformableMeshDependencies;
        private Vector3 _origin;
        private List<Vector3> _vertices = new();
        private HashSet<int> _brushVertexSet = new();
        private Mesh _mesh;
        private Vector3? _lastHitPoint;
        private IReadOnlyList<Vector3> _cachedInterpolatedPoints;
        private bool _leftMouseButtonDown;
        private bool _rightMouseButtonDown;

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
            _dataModel.Material.SetFloat(Property, _dataModel.MaxDepth); // Bind max depth to shader
            GenerateMesh();
        }

        private void OnDrawGizmos()
        {
            if (!_dataModel.DrawGizmos) { return; }

            Gizmos.color = Color.cyan;
            foreach (Vector3 vert in _vertices) Gizmos.DrawSphere(vert, 0.01f);

            if (_brushVertexSet is not { Count: > 0 }) { return; }

            var grad = new Gradient
            {
                colorKeys = new GradientColorKey[]
                {
                    new() { color = Color.red, time = 0.0f },
                    new() { color = Color.green, time = 1.0f }
                },
                alphaKeys = new GradientAlphaKey[]
                {
                    new() { alpha = 1.0f, time = 0.0f },
                    new() { alpha = 1.0f, time = 1.0f }
                }
            };
            int count = _cachedInterpolatedPoints.Count;
            for (int i = 0; i < count; i++)
            {
                float t = count > 1 ? (float)i / count : 0.0f;
                Gizmos.color = grad.Evaluate(t);
                Gizmos.DrawSphere(_cachedInterpolatedPoints[i], 0.01f);
            }
        }
        
        private void OnValidate()
        {
            _dataModel.Material?.SetFloat(Property, _dataModel.MaxDepth);
        }

        private void OnGUI()
        {
            if (_brushVertexSet.Count > 0 || !_lastHitPoint.HasValue) { InvalidateMesh(); }
        }

        private void Update()
        {
            if (Input.GetMouseButtonUp(0)) { _leftMouseButtonDown = false; }
            else if (Input.GetMouseButtonDown(0))
            {
                _leftMouseButtonDown = true;
                _lastHitPoint = null;
            }
            
            if (Input.GetMouseButtonUp(1)) { _rightMouseButtonDown = false; }
            else if (Input.GetMouseButtonDown(1))
            {
                _rightMouseButtonDown = true;
                _lastHitPoint = null;
            }

            ApplyHeal();
            HandleMouseInput();
        }

        private void FixedUpdate()
        {
            _mesh.RecalculateNormals();
        }

        #endregion

        private void HandleMouseInput()
        {
            _brushVertexSet.Clear();
            if (Input.GetMouseButtonUp(0) || Input.GetMouseButtonUp(1)) { return; }

            if (!_leftMouseButtonDown && !_rightMouseButtonDown) { return; }

            Vector3 mousePos = Input.mousePosition;

            Ray ray = _deformableMeshDependencies.MainCamera.ScreenPointToRay(mousePos);

            if (!Physics.SphereCast(ray, _dataModel.BrushRadius, out RaycastHit hitInfo, Mathf.Infinity, _dataModel.HitLayerMask))
            {
                return;
            }

            _cachedInterpolatedPoints = GetInterpolatedPoints(_lastHitPoint, hitInfo.point);
            _brushVertexSet = new HashSet<int>();
            foreach (Vector3 point in _cachedInterpolatedPoints)
            {
                var brushedVerts = GetVerticesInRadius(point, _dataModel.BrushRadius);
                foreach (int vert in brushedVerts) _brushVertexSet.Add(vert);
            }

            float deformDelta = _dataModel.DeformSpeed * Time.deltaTime;
            float max = _dataModel.MaxDepth;
            
            float direction = 0;
            if (LeftMouseDownExclusive) { direction = -1; }
            else if (RightMouseDownExclusive) { direction = 1;}
            foreach (int index in _brushVertexSet)
            {
                _vertices[index] += Vector3.forward * (direction * deformDelta);
                float clampedHeight = Mathf.Clamp(_vertices[index].z, -max, max);
                _vertices[index] = new Vector3(_vertices[index].x, _vertices[index].y, clampedHeight);
            }

            _lastHitPoint = hitInfo.point;
        }

        private void InvalidateMesh()
        {
            _mesh.SetVertices(_vertices);
            _mesh.RecalculateBounds();
        }

        #region Private Methods

        private void ApplyHeal()
        {
            for (int i = 0; i < _vertices.Count; i++)
            {
                if (_brushVertexSet.Contains(i)) { continue; } // Don't heal if we're brushing those verts 

                float height = _vertices[i].z;
                switch (height)
                {
                    case < 0f:
                    {
                        height += _dataModel.HealingRate * Time.deltaTime;
                        if (height >= 0f) { height = 0f; }
                        break;
                    }
                    case > 0f:
                    {
                        height -= _dataModel.HealingRate * Time.deltaTime;
                        if (height <= 0f) { height = 0f; }
                        break;
                    }
                    default:
                        continue;
                }

                _vertices[i] = new Vector3(_vertices[i].x, _vertices[i].y, height);
            }

            _mesh.SetVertices(_vertices);
            _mesh.RecalculateBounds();
        }

        private List<int> GetVerticesInRadius(Vector3 hitPoint, float brushRadius)
        {
            List<int> brushedVertices = new();
            for (int i = 0; i < _vertices.Count; i++)
            {
                Vector3 worldVert = transform.TransformPoint(_vertices[i]);
                if (!_lastHitPoint.HasValue)
                {
                    if (Vector2.Distance(worldVert, hitPoint) <= brushRadius) { brushedVertices.Add(i); }
                }
                else
                {
                    float distance = DistanceFromPointToLineSegment(worldVert, _lastHitPoint.Value, hitPoint);
                    if (distance <= brushRadius) { brushedVertices.Add(i); }
                }
            }

            return brushedVertices;
        }

        private List<Vector3> GetInterpolatedPoints(Vector3? a, Vector3 b)
        {
            if (!a.HasValue) { return new List<Vector3> { b }; }

            List<Vector3> points = new();
            for (int i = 0; i < _dataModel.BrushInterpolationSegments; i++)
                points.Add(Vector3.Slerp(a.Value, b, (float)i / _dataModel.BrushInterpolationSegments));

            points.Add(b); // make sure we have our actual point in the list
            return points;
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
            _mesh.RecalculateNormals();
            _mesh.RecalculateTangents();
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
                triangles[triangleIndex++] = botRight;

                triangles[triangleIndex++] = topLeft;
                triangles[triangleIndex++] = topRight;
                triangles[triangleIndex++] = botRight;
            }

            return triangles;
        }

        private float DistanceFromPointToLineSegment(Vector2 point, Vector2 linePoint1, Vector2 linePoint2)
        {
            Vector2 lineVector = linePoint2 - linePoint1;
            Vector2 pointToLineStart = point - linePoint1;

            float lineLengthSquared = lineVector.sqrMagnitude; // Avoid unnecessary sqrt

            if (lineLengthSquared == 0f)
            {
                return pointToLineStart.magnitude; // The segment is just a point
            }

            // Compute the t parameter of the closest point on the line
            float t = Vector2.Dot(pointToLineStart, lineVector) / lineLengthSquared;
            t = Mathf.Clamp(t, 0f, 1f); // Clamp to stay within the segment

            // Compute the closest point on the segment
            Vector2 closestPoint = linePoint1 + t * lineVector;

            // Return distance from the point to the closest point on the segment
            return Vector2.Distance(point, closestPoint);
        }

        #endregion
    }
}