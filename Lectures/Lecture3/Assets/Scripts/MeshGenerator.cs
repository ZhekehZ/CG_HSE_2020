using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(MeshFilter))]
public class MeshGenerator : MonoBehaviour
{
    public Transform[] Balls = new Transform[0];
    public float BallRadius = 1.0f;
    public ComputeShader computeShader;

    private const int W = 24, H = 24, D = 24;
    private const int TOTAL_CUBES = W * H * D * 3;

    private MeshFilter _filter;
    private Mesh _mesh;

    private struct Point { Vector3 position, normal; };

    private Point[] vertices = new Point[TOTAL_CUBES * 5 * 3];
    private int[] indices = new int[TOTAL_CUBES * 5 * 3];

    private int kernel;

    private ComputeBuffer vertex_buffer;
    private ComputeBuffer counter;

    private VertexAttributeDescriptor[] layout = new[] {
            new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
            new VertexAttributeDescriptor(VertexAttribute.Normal,   VertexAttributeFormat.Float32, 3)
        };

    private void Awake()
    {
        _filter = GetComponent<MeshFilter>();
        _mesh = _filter.mesh = new Mesh();
        _mesh.MarkDynamic();

        vertex_buffer = new ComputeBuffer(TOTAL_CUBES * 5, 3 * 3 * 2 * sizeof(float), ComputeBufferType.Append);
        counter = new ComputeBuffer (1, sizeof(int), ComputeBufferType.Raw);

        kernel = computeShader.FindKernel("BuildShape");

        computeShader.SetBuffer(kernel, "vertices", vertex_buffer);

        for (int i = 0; i < indices.Length; ++i) indices[i] = i;
    }

    private void Update()
    {
        float cubeSize = 0.15f * BallRadius;
        Vector3 centreShift = new Vector3(W, H, D) / 2 * cubeSize;

        Vector3[] positions = Balls.Select(x => 
                                new Vector3(Mathf.Sin(Time.time + x.position.x) * 2,
                                            Mathf.Sin(Time.time + x.position.y) * 2,
                                            Mathf.Sin(Time.time + x.position.z) * 2)).ToArray();
        
        computeShader.SetFloat("cubeSize", cubeSize);
        computeShader.SetVector("centreShift", centreShift);
        computeShader.SetVector("point1", positions[0]);
        computeShader.SetVector("point2", positions[1]);
        computeShader.SetVector("point3", positions[2]);
        computeShader.SetFloat("ballRadius", BallRadius);

        vertex_buffer.SetCounterValue(0);
        computeShader.Dispatch(kernel, W/8, H/8, D/8*3);
        
        int[] c = {0};
        ComputeBuffer.CopyCount(vertex_buffer, counter, 0);
        counter.GetData(c);
        int N = c[0] * 3;
        vertex_buffer.GetData(vertices);

        _mesh.Clear();
        _mesh.SetVertexBufferParams(N, layout);
        _mesh.SetVertexBufferData(vertices, 0, 0, N);
        _mesh.SetTriangles(indices, 0, N, 0);
        _mesh.UploadMeshData(false);
    }

    private void OnDestroy()
    {
        vertex_buffer.Dispose();
        counter.Dispose();
    }
}