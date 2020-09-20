using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using Unity.Mathematics;
using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
public class MeshGenerator : MonoBehaviour
{
    public MetaBallField Field = new MetaBallField();

    private MeshFilter _filter;
    private Mesh _mesh;

    private List<Vector3> vertices = new List<Vector3>();
    private List<Vector3> normals = new List<Vector3>();
    private List<int> indices = new List<int>();

    private const int H = 50, W = 50, D = 50;
    private const float cubeSize = 0.2f;
    private float[,,] grid = new float[W, H, D];
    private Vector3 dx = new Vector3(0.1f, 0, 0);
    private Vector3 dy = new Vector3(0, 0, 0.1f);
    private Vector3 dz = new Vector3(0, 0.1f, 0);

    /// <summary>
    /// Executed by Unity upon object initialization. <see cref="https://docs.unity3d.com/Manual/ExecutionOrder.html"/>
    /// </summary>
    private void Awake()
    {
        // Getting a component, responsible for storing the mesh
        _filter = GetComponent<MeshFilter>();

        // instantiating the mesh
        _mesh = _filter.mesh = new Mesh();

        // Just a little optimization, telling unity that the mesh is going to be updated frequently
        _mesh.MarkDynamic();

    }

    private float[] F = new float[8];

    private Vector3 getPoint(int e) {
        int a = MarchingCubes.Tables._cubeEdges[e][0];
        int b = MarchingCubes.Tables._cubeEdges[e][1];

        return (MarchingCubes.Tables._cubeVertices[a] * F[b] -
                MarchingCubes.Tables._cubeVertices[b] * F[a]) / (F[b] - F[a]);
    }

    private Vector3 getNormal(Vector3 x)
    {
        Vector3 normal = new Vector3(
            Field.F(x + dx) - Field.F(x - dx), 
            Field.F(x + dy) - Field.F(x - dy), 
            Field.F(x + dz) - Field.F(x - dz)
        );
        return Vector3.Normalize(normal);
    }

    private void processCube(Vector3 shift)
    {
        int idx = 0;
        for (int i = 0; i < 8; ++i)
        {
            idx |= (F[i] > 0 ? 1 : 0) * (1 << i);
        }

        byte nTriangles = MarchingCubes.Tables.CaseToTrianglesCount[idx];

        for (int t = 0; t < nTriangles; ++t)
        {
            int3 triangle = MarchingCubes.Tables.CaseToVertices[idx][t];

            indices.Add(vertices.Count);
            vertices.Add(shift + getPoint(triangle.x) * cubeSize);
            normals.Add(getNormal(vertices.Last()));

            indices.Add(vertices.Count);
            vertices.Add(shift + getPoint(triangle.y) * cubeSize);
            normals.Add(getNormal(vertices.Last()));

            indices.Add(vertices.Count);
            vertices.Add(shift + getPoint(triangle.z) * cubeSize);
            normals.Add(getNormal(vertices.Last()));
        }
    }

    /// <summary>
    /// Executed by Unity on every frame <see cref="https://docs.unity3d.com/Manual/ExecutionOrder.html"/>
    /// You can use it to animate something in runtime.
    /// </summary>
    private void Update()
    {

        vertices.Clear();
        indices.Clear();
        normals.Clear();

        Field.Update();

        Vector3 startPos = Field.getCentre() - new Vector3(W, H, D) * cubeSize / 2;


        for (int i = 0; i < W; ++i)
        {
            for (int j = 0; j < H; ++j)
            {
                for (int d = 0; d < D; ++d)
                {
                    Vector3 point = new Vector3(i, j, d) * cubeSize + startPos; 
                    grid[i, j, d] = Field.F(point);
                }
            }
        }

        for (int i = 0; i < W - 1; ++i) 
        {
            for (int j = 0; j < H - 1; ++j) 
            {
                for (int d = 0; d < D - 1; ++d) 
                {
                    F[0] = grid[    i,     j,     d];
                    F[1] = grid[    i, j + 1,     d];
                    F[2] = grid[i + 1, j + 1,     d];
                    F[3] = grid[i + 1,     j,     d];
                    F[4] = grid[    i,     j, d + 1];
                    F[5] = grid[    i, j + 1, d + 1];
                    F[6] = grid[i + 1, j + 1, d + 1];
                    F[7] = grid[i + 1,     j, d + 1];

                    Vector3 shift = new Vector3(i, j, d) * cubeSize + startPos;
                    processCube(shift);
                }
            }
        }

        // Here unity automatically assumes that vertices are points and hence (x, y, z) will be represented as (x, y, z, 1) in homogenous coordinates
        _mesh.Clear();
        _mesh.SetVertices(vertices);
        _mesh.SetTriangles(indices, 0);
        _mesh.SetNormals(normals);

        // Upload mesh data to the GPU
        _mesh.UploadMeshData(false);
    }
}