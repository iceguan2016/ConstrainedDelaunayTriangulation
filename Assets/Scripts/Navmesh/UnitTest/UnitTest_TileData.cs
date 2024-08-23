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
    public int MinBuildTileIndex = -1;   // Only build tiles between MinBuildTileIndex and MaxBuildTileIndex
    public int MaxBuildTileIndex = -1;

    public bool IsShowNodeConnection = false;
    public Navmesh.GraphDebugMode  DebugMode = Navmesh.GraphDebugMode.Areas;
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

    [Tooltip("Toggle whether use tiles")]
    public bool IsUseTiles = true;

    [Tooltip("Voxel cell size")]
    public float CellSize = 0.5f;

    [Tooltip("Numbers of voxel cell for Tile")]
    public int TileSize = 128;

    private Navmesh.FNavgationSystem navgationSystem = null;

    private Navmesh.FTileBuilder tileBuilder;
    public Navmesh.FTileBuilder TileBuilder { get { return tileBuilder; } }

    public Navmesh.FTiledNavmeshBuilder tiledNavmeshBuilder;
    protected Navmesh.FTiledNavmeshGraph tiledNavmeshGraph = null;

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

    private void Awake()
    {
        navgationSystem = new Navmesh.FNavgationSystem();
        navgationSystem.Awake();
    }

    // Start is called before the first frame update
    void Start()
    {
        Triangulation();
    }

    void Update()
    {
        navgationSystem.debugMode = DebugParams.DebugMode;    
    }

    public void Triangulation()
    {
        if (IsUseTiles)
        {
            tiledNavmeshBuilder = new Navmesh.FTiledNavmeshBuilder();

            Navmesh.FTiledNavmeshBuilder.FTiledNavmeshBuilderParams builderParams;
            builderParams.CellSize = CellSize;
            builderParams.TileSize = TileSize;
            builderParams.MinBounds = transform.position - HalfExtent;
            builderParams.MaxBounds = transform.position + HalfExtent;
            builderParams.Obstacles = new List<FObstacle>(Obstacles);

            tiledNavmeshGraph = tiledNavmeshBuilder.Build(builderParams, DebugParams);
        }
        else
        {
            tileBuilder = new Navmesh.FTileBuilder();

            Navmesh.FTileBuilder.FInitTileBuilderParams Params;
            Params.TileX = 0;
            Params.TileZ = 0;
            Params.MinBounds = transform.position - HalfExtent;
            Params.MaxBounds = transform.position + HalfExtent;

            if (tileBuilder.Initialize(Params))
            {
                for (int i = 0; i < Obstacles.Length; ++i)
                {
                    if (!Obstacles[i].IsAdd) continue;
                    var Convex = Obstacles[i].Shape.GetConvexShape();
                    tileBuilder.AddConvexShape(Convex, DebugParams);
                }

                var startTime = Time.realtimeSinceStartupAsDouble;

                if (tileBuilder.Triangulate(DebugParams))
                {
                    var endTime = Time.realtimeSinceStartupAsDouble;

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
    }

    private void OnDrawGizmos()
    {
        var OldColor = Gizmos.color;
        
        Gizmos.color = Color.white;
        Gizmos.DrawWireCube(transform.position, HalfExtent*2);

        if (IsUseTiles)
        {
            // Draw tile rects
            var minBounds = transform.position - HalfExtent;
            var maxBounds = transform.position + HalfExtent;
            var boundSize = maxBounds - minBounds;

            // Voxel grid size
            int gw = (int)(boundSize.x / CellSize + 0.5f);
            int gd = (int)(boundSize.z / CellSize + 0.5f);

            var tileSizeX = TileSize;
            var tileSizeZ = TileSize;

            // Number of tiles
            int tw = (gw + tileSizeX - 1) / tileSizeX;
            int td = (gd + tileSizeZ - 1) / tileSizeZ;

            var tileXCount = tw;
            var tileZCount = td;

            for (int z = 0; z < tileZCount; ++z)
			{
				for (int x = 0; x < tileXCount; ++x)
				{
					// Calculate tile bounds
                    var tileIndex = x + z * tileXCount;
                    
					// World size of tile
					var tcsx = tileSizeX * CellSize;
					var tcsz = tileSizeZ * CellSize;

					var bounds = new UnityEngine.Bounds();
					bounds.SetMinMax(new UnityEngine.Vector3(x * tcsx, 0, z * tcsz) + minBounds,
								new UnityEngine.Vector3((x + 1) * tcsx + minBounds.x, minBounds.y, (z + 1) * tcsz + minBounds.z)
						);

                    var c = (DebugParams.MinBuildTileIndex <= tileIndex && tileIndex <= DebugParams.MaxBuildTileIndex) ? Color.red : Color.yellow;
                    Gizmos.color = c;
                    Gizmos.DrawWireCube(bounds.center, bounds.extents * 2);
                }
            }

            if (null != tiledNavmeshGraph)
            {
                tiledNavmeshGraph.OnDrawGizmos(true);
            }

            //if (null != tiledNavmeshBuilder)
            //{
            //    tiledNavmeshBuilder.DrawGizmos(DebugParams);
            //}
        }
        else
        {
            if (null != tileBuilder) tileBuilder.DrawGizmos(DebugParams);
        }

        Gizmos.color = OldColor;
    }

    protected bool CheckTileTriangulationResult()
    {
        if (null == tileBuilder) return false;
        var MergedContours = tileBuilder.MergedContours;
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
