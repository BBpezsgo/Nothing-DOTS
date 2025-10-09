using System;
using System.Linq;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;

public static class MeshGenerator
{
    static readonly ProfilerMarker _marker = new("Terrain.MeshGenerator");

    public static MeshData GenerateTerrainMesh(in NativeArray<float>.ReadOnly heightMap, int heightMapWidth, int levelOfDetail, Allocator allocator = Allocator.Temp)
    {
        using var _ = _marker.Auto();

        int skipIncrement = Math.Clamp(levelOfDetail * 2, 1, 4);

        Vector2 topLeft = new Vector2(-1, 1) * TerrainSystemServer.MeshWorldSize / 2f;

        MeshData meshData = new(skipIncrement, allocator);

        int[] vertexIndicesMap = new int[TerrainSystemServer.NumVertsPerLine * TerrainSystemServer.NumVertsPerLine];
        int meshVertexIndex = 0;
        int outOfMeshVertexIndex = -1;

        for (int y = 0; y < TerrainSystemServer.NumVertsPerLine; y++)
        {
            for (int x = 0; x < TerrainSystemServer.NumVertsPerLine; x++)
            {
                bool isOutOfMeshVertex = y == 0 || y == TerrainSystemServer.NumVertsPerLine - 1 || x == 0 || x == TerrainSystemServer.NumVertsPerLine - 1;
                bool isSkippedVertex = x > 2 && x < TerrainSystemServer.NumVertsPerLine - 3 && y > 2 && y < TerrainSystemServer.NumVertsPerLine - 3 && ((x - 2) % skipIncrement != 0 || (y - 2) % skipIncrement != 0);

                if (isOutOfMeshVertex)
                {
                    vertexIndicesMap[x + y * TerrainSystemServer.NumVertsPerLine] = outOfMeshVertexIndex;
                    outOfMeshVertexIndex--;
                }
                else if (!isSkippedVertex)
                {
                    vertexIndicesMap[x + y * TerrainSystemServer.NumVertsPerLine] = meshVertexIndex;
                    meshVertexIndex++;
                }
            }
        }

        for (int y = 0; y < TerrainSystemServer.NumVertsPerLine; y++)
        {
            for (int x = 0; x < TerrainSystemServer.NumVertsPerLine; x++)
            {
                bool isSkippedVertex = x > 2 && x < TerrainSystemServer.NumVertsPerLine - 3 && y > 2 && y < TerrainSystemServer.NumVertsPerLine - 3 && ((x - 2) % skipIncrement != 0 || (y - 2) % skipIncrement != 0);
                if (isSkippedVertex) continue;

                bool isOutOfMeshVertex = y == 0 || y == TerrainSystemServer.NumVertsPerLine - 1 || x == 0 || x == TerrainSystemServer.NumVertsPerLine - 1;
                bool isMeshEdgeVertex = (y == 1 || y == TerrainSystemServer.NumVertsPerLine - 2 || x == 1 || x == TerrainSystemServer.NumVertsPerLine - 2) && !isOutOfMeshVertex;
                bool isMainVertex = (x - 2) % skipIncrement == 0 && (y - 2) % skipIncrement == 0 && !isOutOfMeshVertex && !isMeshEdgeVertex;
                bool isEdgeConntectionVertex = (y == 2 || y == TerrainSystemServer.NumVertsPerLine - 3 || x == 2 || x == TerrainSystemServer.NumVertsPerLine - 3) && !isOutOfMeshVertex && !isMeshEdgeVertex && !isMainVertex;

                int vertexIndex = vertexIndicesMap[x + y * TerrainSystemServer.NumVertsPerLine];
                Vector2 percent = new Vector2(x - 1, y - 1) / (TerrainSystemServer.NumVertsPerLine - 3);
                Vector2 vertexPosition2D = topLeft + new Vector2(percent.x, -percent.y) * TerrainSystemServer.MeshWorldSize;
                float height = heightMap[x + y * heightMapWidth];

                if (isEdgeConntectionVertex)
                {
                    bool isVertical = x is 2 or (TerrainSystemServer.NumVertsPerLine - 3);
                    int dstToMainVertexA = (isVertical ? y - 2 : x - 2) % skipIncrement;
                    int dstToMainVertexB = skipIncrement - dstToMainVertexA;
                    float dstPercentFromAToB = dstToMainVertexA / (float)skipIncrement;

                    float heightMainVertexA = heightMap[(isVertical ? x : x - dstToMainVertexA) + (isVertical ? y - dstToMainVertexA : y) * heightMapWidth];
                    float heightMainVertexB = heightMap[(isVertical ? x : x + dstToMainVertexB) + (isVertical ? y + dstToMainVertexB : y) * heightMapWidth];

                    height = heightMainVertexA * (1 - dstPercentFromAToB) + heightMainVertexB * dstPercentFromAToB;
                }

                meshData.AddVertex(new float3(vertexPosition2D.x, height, vertexPosition2D.y), new Vector2(x, y) / (TerrainSystemServer.NumVertsPerLine - 1), vertexIndex);

                bool createTriangle = x < TerrainSystemServer.NumVertsPerLine - 1 && y < TerrainSystemServer.NumVertsPerLine - 1 && (!isEdgeConntectionVertex || (x != 2 && y != 2));

                if (createTriangle)
                {
                    int currentIncrement = (isMainVertex && x != TerrainSystemServer.NumVertsPerLine - 3 && y != TerrainSystemServer.NumVertsPerLine - 3) ? skipIncrement : 1;

                    int a = vertexIndicesMap[x + y * TerrainSystemServer.NumVertsPerLine];
                    int b = vertexIndicesMap[x + currentIncrement + y * TerrainSystemServer.NumVertsPerLine];
                    int c = vertexIndicesMap[x + (y + currentIncrement) * TerrainSystemServer.NumVertsPerLine];
                    int d = vertexIndicesMap[x + currentIncrement + (y + currentIncrement) * TerrainSystemServer.NumVertsPerLine];
                    meshData.AddTriangle(a, d, c);
                    meshData.AddTriangle(d, a, b);
                }
            }
        }

        return meshData;
    }

    public struct MeshData : IDisposable
    {
        NativeArray<float3> Vertices;
        NativeArray<int> Indices;
        NativeArray<float2> UVs;

        NativeArray<float3> OutOfMeshVertices;
        NativeArray<int> OutOfMeshTriangles;

        int TriangleIndex;
        int OutOfMeshTriangleIndex;

        public MeshData(int skipIncrement, Allocator allocator = Allocator.Temp)
        {
            const int numMeshEdgeVertices = (TerrainSystemServer.NumVertsPerLine - 2) * 4 - 4;
            int numEdgeConnectionVertices = (skipIncrement - 1) * (TerrainSystemServer.NumVertsPerLine - 5) / skipIncrement * 4;
            int numMainVerticesPerLine = (TerrainSystemServer.NumVertsPerLine - 5) / skipIncrement + 1;
            int numMainVertices = numMainVerticesPerLine * numMainVerticesPerLine;

            Vertices = new(numMeshEdgeVertices + numEdgeConnectionVertices + numMainVertices, allocator);
            UVs = new(Vertices.Length, allocator);

            int numMeshEdgeTriangles = ((TerrainSystemServer.NumVertsPerLine - 3) * 4 - 4) * 2;
            int numMainTriangles = (TerrainSystemServer.NumVertsPerLine - 1) * (TerrainSystemServer.NumVertsPerLine - 1) * 2;

            Indices = new((numMeshEdgeTriangles + numMainTriangles) * 3, allocator);

            OutOfMeshVertices = new(TerrainSystemServer.NumVertsPerLine * 4 - 4, allocator);
            OutOfMeshTriangles = new(((TerrainSystemServer.NumVertsPerLine - 1) * 4 - 4) * 2 * 3, allocator);
        }

        public void AddVertex(float3 vertexPosition, float2 uv, int vertexIndex)
        {
            if (vertexIndex < 0)
            {
                OutOfMeshVertices[-vertexIndex - 1] = vertexPosition;
            }
            else
            {
                Vertices[vertexIndex] = vertexPosition;
                UVs[vertexIndex] = uv;
            }
        }

        public void AddTriangle(int a, int b, int c)
        {
            if (a < 0 || b < 0 || c < 0)
            {
                OutOfMeshTriangles[OutOfMeshTriangleIndex] = a;
                OutOfMeshTriangles[OutOfMeshTriangleIndex + 1] = b;
                OutOfMeshTriangles[OutOfMeshTriangleIndex + 2] = c;
                OutOfMeshTriangleIndex += 3;
            }
            else
            {
                Indices[TriangleIndex] = a;
                Indices[TriangleIndex + 1] = b;
                Indices[TriangleIndex + 2] = c;
                TriangleIndex += 3;
            }
        }

        readonly NativeArray<float3> CalculateNormals(Allocator allocator = Allocator.Temp)
        {
            NativeArray<float3> vertexNormals = new(Vertices.Length, allocator);

            int triangleCount = Indices.Length / 3;
            for (int i = 0; i < triangleCount; i++)
            {
                int normalTriangleIndex = i * 3;
                int vertexIndexA = Indices[normalTriangleIndex];
                int vertexIndexB = Indices[normalTriangleIndex + 1];
                int vertexIndexC = Indices[normalTriangleIndex + 2];

                float3 triangleNormal = SurfaceNormalFromIndices(vertexIndexA, vertexIndexB, vertexIndexC);
                vertexNormals[vertexIndexA] += triangleNormal;
                vertexNormals[vertexIndexB] += triangleNormal;
                vertexNormals[vertexIndexC] += triangleNormal;
            }

            int borderTriangleCount = OutOfMeshTriangles.Length / 3;
            for (int i = 0; i < borderTriangleCount; i++)
            {
                int normalTriangleIndex = i * 3;
                int vertexIndexA = OutOfMeshTriangles[normalTriangleIndex];
                int vertexIndexB = OutOfMeshTriangles[normalTriangleIndex + 1];
                int vertexIndexC = OutOfMeshTriangles[normalTriangleIndex + 2];

                float3 triangleNormal = SurfaceNormalFromIndices(vertexIndexA, vertexIndexB, vertexIndexC);
                if (vertexIndexA >= 0)
                {
                    vertexNormals[vertexIndexA] += triangleNormal;
                }
                if (vertexIndexB >= 0)
                {
                    vertexNormals[vertexIndexB] += triangleNormal;
                }
                if (vertexIndexC >= 0)
                {
                    vertexNormals[vertexIndexC] += triangleNormal;
                }
            }

            for (int i = 0; i < vertexNormals.Length; i++)
            {
                vertexNormals[i] = math.normalize(vertexNormals[i]);
            }

            vertexNormals[0] = vertexNormals[1];

            return vertexNormals;

        }

        readonly float3 SurfaceNormalFromIndices(int indexA, int indexB, int indexC)
        {
            float3 pointA = (indexA < 0) ? OutOfMeshVertices[-indexA - 1] : Vertices[indexA];
            float3 pointB = (indexB < 0) ? OutOfMeshVertices[-indexB - 1] : Vertices[indexB];
            float3 pointC = (indexC < 0) ? OutOfMeshVertices[-indexC - 1] : Vertices[indexC];

            float3 sideAB = pointB - pointA;
            float3 sideAC = pointC - pointA;
            return math.normalize(math.cross(sideAB, sideAC));
        }

        void FlatShading(Allocator allocator = Allocator.Temp)
        {
            NativeArray<float3> flatShadedVertices = new(Indices.Length, allocator, NativeArrayOptions.UninitializedMemory);
            NativeArray<float2> flatShadedUvs = new(Indices.Length, allocator, NativeArrayOptions.UninitializedMemory);

            for (int i = 0; i < Indices.Length; i++)
            {
                flatShadedVertices[i] = Vertices[Indices[i]];
                flatShadedUvs[i] = UVs[Indices[i]];
                Indices[i] = i;
            }

            Vertices.Dispose();
            UVs.Dispose();

            Vertices = flatShadedVertices;
            UVs = flatShadedUvs;
        }

        public Mesh.MeshDataArray CreateNativeMesh(Allocator allocator = Allocator.Temp)
        {
            NativeArray<float3> normals;

            if (TerrainSystemServer.useFlatShading)
            {
                normals = default;
                FlatShading(allocator);
            }
            else
            {
                normals = CalculateNormals(allocator);
            }

            Mesh.MeshDataArray meshDataArray = Mesh.AllocateWritableMeshData(1);
            Mesh.MeshData meshData = meshDataArray[0];

            NativeArray<VertexAttributeDescriptor> attributes = new(3, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            attributes[0] = new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3, 0);
            attributes[1] = new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3, 1);
            attributes[2] = new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2, 2);

            meshData.SetVertexBufferParams(Vertices.Length, attributes);
            meshData.SetIndexBufferParams(Indices.Length, IndexFormat.UInt32);

            meshData.GetVertexData<float3>(0).CopyFrom(Vertices);
            if (!TerrainSystemServer.useFlatShading) meshData.GetVertexData<float3>(1).CopyFrom(normals);
            meshData.GetVertexData<float2>(2).CopyFrom(UVs);
            meshData.GetIndexData<int>().CopyFrom(Indices);

            meshData.subMeshCount = 1;
            meshData.SetSubMesh(0, new SubMeshDescriptor(0, Indices.Length));

            return meshDataArray;
        }

        public Mesh CreateManagedMesh(Allocator allocator = Allocator.Temp)
        {
            NativeArray<float3> normals;

            if (TerrainSystemServer.useFlatShading)
            {
                normals = default;
                FlatShading(allocator);
            }
            else
            {
                normals = CalculateNormals(allocator);
            }

            Mesh newMesh = new()
            {
                vertices = Vertices.Select(v => (Vector3)v).ToArray(),
                triangles = Indices.ToArray(),
                uv = UVs.Select(v => (Vector2)v).ToArray(),
            };
            if (!TerrainSystemServer.useFlatShading) newMesh.normals = normals.Select(v => (Vector3)v).ToArray();

            normals.Dispose();

            return newMesh;
        }

        public void Dispose()
        {
            Vertices.Dispose();
            Indices.Dispose();
            UVs.Dispose();

            OutOfMeshVertices.Dispose();
            OutOfMeshTriangles.Dispose();
        }
    }
}
