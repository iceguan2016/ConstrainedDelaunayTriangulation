using Game.Utils.Math;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UnitTest_TileData : MonoBehaviour
{
    // ≈‰÷√≤Œ ˝
    [Tooltip("The size of tile.")]
    public Vector3      HalfExtent = new Vector3(5.0f, 5.0f, 5.0f);
    [Tooltip("The constraint shapes.")]
    public BoxShape[]   Obstacles = new BoxShape[0];

    [Tooltip("The mesh that displays the output triangles.")]
    public MeshFilter VisualRepresentation;

    private Navmesh.FTileData tileData;
    public Navmesh.FTileData TileData { get { return tileData; } }

    private List<Triangle2D> triangle2Ds = new List<Triangle2D>();
    public List<Triangle2D> Triangle2Ds { get { return triangle2Ds; } }

    private Mesh CreateMeshFromTriangles(List<Triangle2D> triangles)
    {
        List<Vector3> vertices = new List<Vector3>(triangles.Count * 3);
        List<int> indices = new List<int>(triangles.Count * 3);

        for (int i = 0; i < triangles.Count; ++i)
        {
            vertices.Add(triangles[i].p0);
            vertices.Add(triangles[i].p1);
            vertices.Add(triangles[i].p2);
            indices.Add(i * 3 + 2); // Changes order
            indices.Add(i * 3 + 1);
            indices.Add(i * 3);
        }

        Mesh mesh = new Mesh();
        mesh.subMeshCount = 1;
        mesh.SetVertices(vertices);
        mesh.SetIndices(indices, MeshTopology.Triangles, 0);
        return mesh;
    }

    // Start is called before the first frame update
    void Start()
    {
        tileData = new Navmesh.FTileData();

        Navmesh.FTileData.FInitTileDataParams Params;
        Params.TileX = 0;
        Params.TileZ = 0;
        Params.MinBounds = transform.position - HalfExtent;
        Params.MaxBounds = transform.position + HalfExtent;

        if (tileData.Initialize(Params))
        {
            for (int i=0; i<Obstacles.Length; ++i)
            {
                var Convex = Obstacles[i].GetConvexShape();
                tileData.AddConvexShape(Convex);
            }

            var startTime = Time.realtimeSinceStartupAsDouble;

            if (tileData.Triangulate())
            {
                var endTime = Time.realtimeSinceStartupAsDouble;

                triangle2Ds.Clear();
                tileData.Triangulation.GetTrianglesDiscardingHoles(triangle2Ds);

                // if (VisualRepresentation != null) VisualRepresentation.mesh = CreateMeshFromTriangles(triangle2Ds);

                var deltaTime = endTime - startTime;
                Debug.Log($"Triangulate cost time: {deltaTime} seconds");
            }
        }
    }

    private void OnDrawGizmos()
    {
        var OldColor = Gizmos.color;
        
        Gizmos.color = Color.white;
        Gizmos.DrawWireCube(transform.position, HalfExtent*2);

        Gizmos.color = Color.yellow;
        for (int i = 0; i < triangle2Ds.Count; ++i)
        {
            var triangle = triangle2Ds[i];
            var v0 = new Vector3(triangle.p0[0], transform.position.y, triangle.p0[1]);
            var v1 = new Vector3(triangle.p1[0], transform.position.y, triangle.p1[1]);
            var v2 = new Vector3(triangle.p2[0], transform.position.y, triangle.p2[1]);
            Gizmos.DrawLine(v0, v1);
            Gizmos.DrawLine(v1, v2);
            Gizmos.DrawLine(v2, v0);
        }

        if (null != tileData) tileData.DrawGizmos();

        Gizmos.color = OldColor;
    }
}
