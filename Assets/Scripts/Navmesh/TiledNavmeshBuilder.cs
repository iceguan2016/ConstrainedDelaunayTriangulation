using System;
using System.Collections.Generic;
using Navmesh.Core;
using Navmesh.Nodes;

namespace Navmesh
{
    public class FTiledNavmeshBuilder
    { 
		protected FTileBuilder[] tileBuilders = null;

        public struct FTiledNavmeshBuilderParams
        {
            public UnityEngine.Vector3 MinBounds;	// 
            public UnityEngine.Vector3 MaxBounds;

			public float	CellSize;	// Size of voxel cell
            public int		TileSize;	// Number of voxel cell along x or z axis for single tile

			public List<FObstacle> Obstacles; // Obstacles in the scence
        }

		public FTiledNavmeshGraph Build(FTiledNavmeshBuilderParams inparams, FDebugParams indebugPramas)
		{
			FTiledNavmeshGraph graph = new FTiledNavmeshGraph();
			graph.showNodeConnections = indebugPramas.IsShowNodeConnection;

			graph.forcedBoundsCenter = (inparams.MinBounds + inparams.MaxBounds) * 0.5f;
			graph.forcedBoundsSize = (inparams.MaxBounds - inparams.MinBounds);

			graph.cellSize = inparams.CellSize;

			// Voxel grid size
			int gw = (int)(graph.forcedBounds.size.x / graph.cellSize + 0.5f);
			int gd = (int)(graph.forcedBounds.size.z / graph.cellSize + 0.5f);

			graph.tileSizeX = inparams.TileSize;
			graph.tileSizeZ = inparams.TileSize;

			// Number of tiles
			int tw = (gw + graph.tileSizeX - 1) / graph.tileSizeX;
			int td = (gd + graph.tileSizeZ - 1) / graph.tileSizeZ;

			graph.tileXCount = tw;
			graph.tileZCount = td;

			if (graph.tileXCount * graph.tileZCount > FTiledNavmeshGraph.TileIndexMask + 1)
			{
				throw new System.Exception("Too many tiles (" + (graph.tileXCount * graph.tileZCount) + ") maximum is " + (FTiledNavmeshGraph.TileIndexMask + 1) +
					"\nTry disabling ASTAR_RECAST_LARGER_TILES under the 'Optimizations' tab in the A* inspector.");
			}

			graph.tiles = new FTiledNavmeshGraph.FNavmeshTile[graph.tileXCount * graph.tileZCount];

			// Initialize all tile builders
			tileBuilders = new FTileBuilder[graph.tileXCount * graph.tileZCount];
			for (int z = 0; z < graph.tileZCount; ++z)
			{
				for (int x = 0; x < graph.tileXCount; ++x)
				{
					var tileIndex = x + z * graph.tileXCount;

					if (indebugPramas.MinBuildTileIndex > tileIndex || tileIndex > indebugPramas.MaxBuildTileIndex) continue;

					// Calculate tile bounds
					var forcedBoundsMin = graph.forcedBounds.min;
					var forcedBoundsMax = graph.forcedBounds.max;

					// World size of tile
					var tcsx = graph.tileSizeX * graph.cellSize;
					var tcsz = graph.tileSizeZ * graph.cellSize;

					var bounds = new UnityEngine.Bounds();
					bounds.SetMinMax(new UnityEngine.Vector3(x * tcsx, 0, z * tcsz) + forcedBoundsMin,
								new UnityEngine.Vector3((x + 1) * tcsx + forcedBoundsMin.x, forcedBoundsMax.y, (z + 1) * tcsz + forcedBoundsMin.z)
						);

					tileBuilders[tileIndex] = CreateTileBuilder(x, z, bounds.min, bounds.max);
				}
			}

			// Add obstacles to tile builders
			if (null != inparams.Obstacles)
			{
				for (int i = 0; i < inparams.Obstacles.Count; ++i)
				{
					var obstacleShape = inparams.Obstacles[i].Shape;
					var convexShape = obstacleShape.GetConvexShape();
					if (null == convexShape || !convexShape.IsValid) continue;

					var bounds = new UnityEngine.Bounds();
					bounds.SetMinMax(convexShape.MinBounds, convexShape.MaxBounds);
					var rect = graph.GetTouchingTiles(bounds);

					for (int z = rect.ymin; z <= rect.ymax; ++z)
					{
						for (int x = rect.xmin; x <= rect.xmax; ++x)
						{
							var tileIndex = x + z * graph.tileXCount;
							var tileBuilder = tileBuilders[tileIndex];
							if (null == tileBuilder) continue;
							tileBuilder.AddConvexShape(convexShape, indebugPramas);
						}
					}
				}
			}

			// Build all tiles
			for (int tileIndex = 0; tileIndex < tileBuilders.Length; ++tileIndex)
			{
				var tileBuilder = tileBuilders[tileIndex];
				if (null == tileBuilder) continue;

				if (tileBuilder.Triangulate(indebugPramas))
				{
					var tileNavmesh = tileBuilder.CreateNavmeshTile(graph);
					if (null != tileNavmesh)
					{
						graph.tiles[tileIndex] = tileNavmesh;
					}
				}
			}

			// Add new graph to NavgationSystem
			FNavgationSystem.instance.AddGraph(graph);
			// Assign correct graph indices.
			var graphIndex = graph.graphIndex;
			TriangleMeshNode.SetNavmeshHolder((int)graphIndex, graph);

			graph.GetNodes(node => {
				node.GraphIndex = graphIndex;
				return true;
			});

			// Connect tile neighbors
			for (int z = 0; z < td; z++)
			{
				for (int x = 0; x < tw; x++)
				{
					if (x < tw - 1) ConnectTiles(graph, graph.tiles[x + z * graph.tileXCount], graph.tiles[x + 1 + z * graph.tileXCount]);
					if (z < td - 1) ConnectTiles(graph, graph.tiles[x + z * graph.tileXCount], graph.tiles[x + (z + 1) * graph.tileXCount]);
				}
			}

			return graph;
		}

		FTileBuilder CreateTileBuilder(int InTileX, int InTileZ, UnityEngine.Vector3 InTileMinBounds, UnityEngine.Vector3 InTileMaxBounds)
        {
			var tileBuilder = new FTileBuilder();
			FTileBuilder.FInitTileBuilderParams initParams;
			initParams.TileX = InTileX;
			initParams.TileZ = InTileZ;
			initParams.MinBounds = InTileMinBounds;
			initParams.MaxBounds = InTileMaxBounds;

			tileBuilder.Initialize(initParams);
            return tileBuilder;
        }

		/** Generate connections between the two tiles.
		 * The tiles must be adjacent.
		 */
		void ConnectTiles(FTiledNavmeshGraph navmeshGraph, FTiledNavmeshGraph.FNavmeshTile tile1, FTiledNavmeshGraph.FNavmeshTile tile2)
		{
			if (tile1 == null) return;//throw new System.ArgumentNullException ("tile1");
			if (tile2 == null) return;//throw new System.ArgumentNullException ("tile2");

			if (tile1.nodes == null) throw new System.ArgumentException("tile1 does not contain any nodes");
			if (tile2.nodes == null) throw new System.ArgumentException("tile2 does not contain any nodes");

			int t1x = UnityEngine.Mathf.Clamp(tile2.x, tile1.x, tile1.x + tile1.w - 1);
			int t2x = UnityEngine.Mathf.Clamp(tile1.x, tile2.x, tile2.x + tile2.w - 1);
			int t1z = UnityEngine.Mathf.Clamp(tile2.z, tile1.z, tile1.z + tile1.d - 1);
			int t2z = UnityEngine.Mathf.Clamp(tile1.z, tile2.z, tile2.z + tile2.d - 1);

			int coord, altcoord;
			int t1coord, t2coord;

			float tcs;

			if (t1x == t2x)
			{
				coord = 2;
				altcoord = 0;
				t1coord = t1z;
				t2coord = t2z;
				tcs = navmeshGraph.tileSizeZ * navmeshGraph.cellSize;
			}
			else if (t1z == t2z)
			{
				coord = 0;
				altcoord = 2;
				t1coord = t1x;
				t2coord = t2x;
				tcs = navmeshGraph.tileSizeX * navmeshGraph.cellSize;
			}
			else
			{
				throw new System.ArgumentException("Tiles are not adjacent (neither x or z coordinates match)");
			}

			if (System.Math.Abs(t1coord - t2coord) != 1)
			{

				UnityEngine.Debug.Log(tile1.x + " " + tile1.z + " " + tile1.w + " " + tile1.d + "\n" +
				tile2.x + " " + tile2.z + " " + tile2.w + " " + tile2.d + "\n" +
				t1x + " " + t1z + " " + t2x + " " + t2z);
				throw new System.ArgumentException("Tiles are not adjacent (tile coordinates must differ by exactly 1. Got '" + t1coord + "' and '" + t2coord + "')");
			}

			//Midpoint between the two tiles
			int midpoint = (int)System.Math.Round((System.Math.Max(t1coord, t2coord) * tcs + navmeshGraph.forcedBounds.min[coord]) * Int3.Precision);

#if ASTARDEBUG
			Vector3 v1 = new Vector3(-100,0,-100);
			Vector3 v2 = new Vector3(100,0,100);
			v1[coord] = midpoint*Int3.PrecisionFactor;
			v2[coord] = midpoint*Int3.PrecisionFactor;

			Debug.DrawLine (v1,v2,Color.magenta);
#endif

			TriangleMeshNode[] nodes1 = tile1.nodes;
			TriangleMeshNode[] nodes2 = tile2.nodes;

			//Find adjacent nodes on the border between the tiles
			for (int i = 0; i < nodes1.Length; i++)
			{
				TriangleMeshNode node = nodes1[i];
				int av = node.GetVertexCount();

				for (int a = 0; a < av; a++)
				{
					Int3 ap1 = node.GetVertex(a);
					Int3 ap2 = node.GetVertex((a + 1) % av);
					if (System.Math.Abs(ap1[coord] - midpoint) < 2 && System.Math.Abs(ap2[coord] - midpoint) < 2)
					{
#if ASTARDEBUG
						Debug.DrawLine ((Vector3)ap1, (Vector3)ap2, Color.red);
#endif

						int minalt = Math.Min(ap1[altcoord], ap2[altcoord]);
						int maxalt = Math.Max(ap1[altcoord], ap2[altcoord]);

						//Degenerate edge
						if (minalt == maxalt) continue;

						for (int j = 0; j < nodes2.Length; j++)
						{
							TriangleMeshNode other = nodes2[j];
							int bv = other.GetVertexCount();
							for (int b = 0; b < bv; b++)
							{
								Int3 bp1 = other.GetVertex(b);
								Int3 bp2 = other.GetVertex((b + 1) % av);
								if (Math.Abs(bp1[coord] - midpoint) < 2 && Math.Abs(bp2[coord] - midpoint) < 2)
								{

									int minalt2 = Math.Min(bp1[altcoord], bp2[altcoord]);
									int maxalt2 = Math.Max(bp1[altcoord], bp2[altcoord]);

									//Degenerate edge
									if (minalt2 == maxalt2) continue;

									if (maxalt > minalt2 && minalt < maxalt2)
									{

										//Adjacent
										// 这里不需要额外验证高度差是否满足爬行高度，因为这里是基于2d构建tile，后续如果有3d需求，需要验证高度差
										// Polygon.DistanceSegmentSegment3D函数就是计算相邻接的2个edge在3d空间的距离（亦即高度差，因为edge相邻不存在x或z方向的差异）
										// 这2个相邻tile的polygon，其相邻edge如何找patrol段的，可以看TriangleMeshNode.GetPortal函数
										uint cost = (uint)(node.position - other.position).costMagnitude;

										node.AddConnection(other, cost);
										other.AddConnection(node, cost);

										////Test shortest distance between the segments (first test if they are equal since that is much faster)
										//if ((ap1 == bp1 && ap2 == bp2) || (ap1 == bp2 && ap2 == bp1) ||
										//	Polygon.DistanceSegmentSegment3D((Vector3)ap1, (Vector3)ap2, (Vector3)bp1, (Vector3)bp2) < walkableClimb * walkableClimb)
										//{

										//	uint cost = (uint)(node.position - other.position).costMagnitude;

										//	node.AddConnection(other, cost);
										//	other.AddConnection(node, cost);
										//}
									}
								}
							}
						}

					}
				}
			}
		}

		public void DrawGizmos(FDebugParams InDebugParams)
		{
			if (null != tileBuilders)
			{
				for (int tileIndex = 0; tileIndex < tileBuilders.Length; ++tileIndex)
				{
					var tileBuilder = tileBuilders[tileIndex];
					if (null == tileBuilder) continue;

					tileBuilder.DrawGizmos(InDebugParams);
				}
			}
		}
	}
}
