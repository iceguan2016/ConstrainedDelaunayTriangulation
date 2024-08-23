using Game.Utils.Math;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class FDebugParams
{
    public bool IsTrangulation = true;
    public bool IsFindContour = true;
    public bool IsDrawConvexShape = true;
    public bool IsDrawConvexPoint = true;
    public float DrawShapeScale = 0.95f;
    public bool IsDrawSharedEdges = false;
    public bool IsDrawMergedEdges = false;
    public bool IsDrawTrangulation = true;

    public int LimitObstacleClipTimes = -1;
}

[System.Serializable]
public class FObstacle
{
    public BoxShape Shape = null;
    public bool     IsAdd = true;
}

public class UnitTest_TileData : MonoBehaviour
{
    // 配置参数
    [Tooltip("The size of tile.")]
    public Vector3      HalfExtent = new Vector3(5.0f, 5.0f, 5.0f);
    [Tooltip("The constraint shapes.")]
    public FObstacle[]  Obstacles = new FObstacle[0];

    [Tooltip("The mesh that displays the output triangles.")]
    public MeshFilter VisualRepresentation;

    [Tooltip("The debug visulize params")]
    public FDebugParams DebugParams;

    private Navmesh.FTileBuilder tileData;
    public Navmesh.FTileBuilder TileData { get { return tileData; } }

    private List<Triangle2D> triangle2Ds = new List<Triangle2D>();
    public List<Triangle2D> Triangle2Ds { get { return triangle2Ds; } }

    private bool IsLastTriangulationSuccess = false;

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
        Triangulation();
    }

    public void Triangulation()
    {
        tileData = new Navmesh.FTileBuilder();
        triangle2Ds.Clear();

        Navmesh.FTileBuilder.FInitTileBuilderParams Params;
        Params.TileX = 0;
        Params.TileZ = 0;
        Params.MinBounds = transform.position - HalfExtent;
        Params.MaxBounds = transform.position + HalfExtent;

        if (tileData.Initialize(Params))
        {
            for (int i = 0; i < Obstacles.Length; ++i)
            {
                if (!Obstacles[i].IsAdd) continue;
                var Convex = Obstacles[i].Shape.GetConvexShape();
                tileData.AddConvexShape(Convex, DebugParams);
            }

            var startTime = Time.realtimeSinceStartupAsDouble;

            if (tileData.Triangulate(DebugParams))
            {
                var endTime = Time.realtimeSinceStartupAsDouble;

                if (tileData.Triangulation != null) tileData.Triangulation.GetTrianglesDiscardingHoles(triangle2Ds);

                // if (VisualRepresentation != null) VisualRepresentation.mesh = CreateMeshFromTriangles(triangle2Ds);
                IsLastTriangulationSuccess = CheckTileTriangulationResult();

                var deltaTime = endTime - startTime;
                Debug.Log($"Triangulate cost time: {deltaTime} seconds");
            }
            else
            {
                IsLastTriangulationSuccess = false;
            }

            if (!IsLastTriangulationSuccess)
            {
                Debug.LogError("IsLastTriangulationSuccess failed!");
            }
        }
    }

    private void OnDrawGizmos()
    {
        var OldColor = Gizmos.color;
        
        Gizmos.color = Color.white;
        Gizmos.DrawWireCube(transform.position, HalfExtent*2);

        if (DebugParams.IsDrawTrangulation)
        {
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
        }

        if (null != tileData) tileData.DrawGizmos(DebugParams);

        Gizmos.color = OldColor;
    }

    protected bool CheckTileTriangulationResult()
    {
        if (null == tileData) return false;
        var MergedContours = tileData.MergedContours;
        if (MergedContours.Count <= 0) return false;

        for (var i = 0; i < MergedContours.Count; ++i)
        {
            var Contour = MergedContours[i];
            // 检查是否首尾相连，且无重叠edge
            for (var edgeIndex = 0; edgeIndex < Contour.Count; ++edgeIndex)
            {
                var edgeIndex1 = (edgeIndex + 1) % Contour.Count;
                var edgeIndex2 = (edgeIndex1 + 1) % Contour.Count;
                var dist = (Contour[edgeIndex] - Contour[edgeIndex2]).magnitude;
                if (dist < 0.001f)
                {
                    return false;
                }
            }
        }
        return true;
    }

    public void RandomTest()
    {
        if (Obstacles.Length <= 0) return;

        if (IsLastTriangulationSuccess)
        {
            //  随机分配Obstacle的位置，并测试
            var MinBounds = transform.position - HalfExtent;
            var MaxBounds = transform.position + HalfExtent;

            for (var i = 0; i < Obstacles.Length; ++i)
            {
                var Obstacle = Obstacles[i];

                var randomXPos = Random.Range(MinBounds.x, MaxBounds.x);
                var randomZPos = Random.Range(MinBounds.z, MaxBounds.z);

                var oldPosition = Obstacle.Shape.transform.position;
                Obstacle.Shape.transform.position = new Vector3(randomXPos, oldPosition.y, randomZPos);

                var randomAngle = Random.Range(0.0f, 360.0f);
                Obstacle.Shape.transform.rotation = Quaternion.Euler(0.0f, randomAngle, 0.0f);
            }
        }

        Triangulation();

        if (IsLastTriangulationSuccess)
        {
            IsLastTriangulationSuccess = CheckTileTriangulationResult();
        }
    }

    public void DumpObstacles()
    {
        for (var i = 0; i < Obstacles.Length; ++i)
        {
            var Obstacle = Obstacles[i];

            Debug.LogError($"Obstacle[{i}], pos:{Obstacle.Shape.transform.position}, rot:{Obstacle.Shape.transform.rotation}");
        }
    }
}
