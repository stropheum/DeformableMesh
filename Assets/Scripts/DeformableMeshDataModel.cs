using System;
using UnityEngine;

namespace MeshToy
{
    [Serializable]
    public struct DeformableMeshDataModel
    {
        [SerializeField] public Vector2Int VertexRange;
        [SerializeField] public float VertexSpacing;
        [SerializeField] public Material Material;
        [SerializeField] public LayerMask HitLayerMask;
        [SerializeField] public float BrushRadius;
        [SerializeField] public float DeformSpeed;
        [SerializeField] public int BrushInterpolationSegments;
        [SerializeField] public bool DrawGizmos;
        [SerializeField] public float HealingRate;
    }

    [Serializable]
    public struct DeformableMeshDependencies
    {
        [SerializeField] public MeshFilter MeshFilter;
        [SerializeField] public MeshCollider MeshCollider;
        [SerializeField] public MeshRenderer MeshRenderer;
        [SerializeField] public Camera MainCamera;
    }
}