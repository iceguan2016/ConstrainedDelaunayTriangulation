using Navmesh.Core;
using Navmesh.Nodes;
using Navmesh.Utils;

namespace Navmesh
{
	public abstract class FNavGraph 
	{
		/** Index of the graph, used for identification purposes */
		public uint graphIndex;

		/** Count nodes in the graph.
		 * Note that this is, unless the graph type has overriden it, an O(n) operation.
		 *
		 * \todo GridGraph should override this
		 */
		public virtual int CountNodes()
		{
			int count = 0;
			GraphNodeDelegateCancelable del = node => {
				count++;
				return true;
			};
			GetNodes(del);
			return count;
		}

		/** Calls a delegate with all nodes in the graph.
		 * This is the primary way of "looping" through all nodes in a graph.
		 *
		 * This function should not change anything in the graph structure.
		 *
		 * \code
		 * myGraph.GetNodes ((node) => {
		 *     Debug.Log ("I found a node at position " + (Vector3)node.Position);
		 *     return true;
		 * });
		 * \endcode
		 */
		public abstract void GetNodes(GraphNodeDelegateCancelable del);

		/**
		 * This will be called on the same time as Awake on the gameObject which the AstarPath script is attached to. (remember, not in the editor)
		 * Use this for any initialization code which can't be placed in Scan
		 */
		public virtual void Awake()
		{
		}

		/** Function for cleaning up references.
		 * This will be called on the same time as OnDisable on the gameObject which the AstarPath script is attached to (remember, not in the editor).
		 * Use for any cleanup code such as cleaning up static variables which otherwise might prevent resources from being collected.
		 * Use by creating a function overriding this one in a graph class, but always call base.OnDestroy () in that function.
		 * All nodes should be destroyed in this function otherwise a memory leak will arise.
		 */
		public virtual void OnDestroy()
		{
			//Destroy all nodes
			GetNodes(delegate (GraphNode node) {
				node.Destroy();
				return true;
			});
		}

		/** Draw gizmos for the graph */
		public virtual void OnDrawGizmos(bool drawNodes)
		{

			if (!drawNodes)
			{
				return;
			}

			GraphNode node = null;

			// Use this delegate to draw connections
			// from the #node variable to #otherNode
			GraphNodeDelegate drawConnection = otherNode => UnityEngine.Gizmos.DrawLine((UnityEngine.Vector3)node.position, (UnityEngine.Vector3)otherNode.position);

			GetNodes(_node => {
				// Set the #node variable so that #drawConnection can use it
				node = _node;

				//Gizmos.color = NodeColor(node, AstarPath.active.debugPathData);
				//if (AstarPath.active.showSearchTree && !InSearchTree(node, AstarPath.active.debugPath)) return true;


				//PathNode nodeR = data != null ? data.GetPathNode(node) : null;
				//if (AstarPath.active.showSearchTree && nodeR != null && nodeR.parent != null)
				//{
				//	Gizmos.DrawLine((Vector3)node.position, (Vector3)nodeR.parent.node.position);
				//}
				//else
				{
					node.GetConnections(drawConnection);
				}
				return true;
			});
		}
	}

    public class FTiledNavmeshGraph : FNavGraph, INavmeshHolder
    {
		/** Center of the bounding box.
		 * Scanning will only be done inside the bounding box */
		public UnityEngine.Vector3 forcedBoundsCenter;

		/** Size of the bounding box. */
		public UnityEngine.Vector3 forcedBoundsSize = new UnityEngine.Vector3(100, 40, 100);

		/** Voxel sample size (x,z) */
		public float cellSize = 0.5F;

		/** Size of a tile along the X axis in voxels.
		 * \warning Do not modify, it is set from #editorTileSize at Scan
		 */
		public int tileSizeX = 128;

		/** Size of a tile along the Z axis in voxels.
		 * \warning Do not modify, it is set from #editorTileSize at Scan
		 */
		public int tileSizeZ = 128;


		public class FNavmeshTile : INavmeshHolder
		{
			/** Tile triangles */
			public int[] tris;

			/** Tile vertices */
			public Int3[] verts;

			/** Tile X Coordinate */
			public int x;

			/** Tile Z Coordinate */
			public int z;

			/** Width, in tile coordinates.
			 * Usually 1.
			 */
			public int w;

			/** Depth, in tile coordinates.
			 * Usually 1.
			 */
			public int d;

			/** All nodes in the tile */
			public TriangleMeshNode[] nodes;

			/** Bounding Box Tree for node lookups */
			// public BBTree bbTree;

			/** Temporary flag used for batching */
			// public bool flag;

			public void GetTileCoordinates(int tileIndex, out int x, out int z)
			{
				x = this.x;
				z = this.z;
			}

			public int GetVertexArrayIndex(int index)
			{
				return index & VertexIndexMask;
			}

			/** Get a specific vertex in the tile */
			public Int3 GetVertex(int index)
			{
				int idx = index & VertexIndexMask;
				return verts[idx];
			}

            public void GetNodes(GraphNodeDelegateCancelable del)
            {
                if (nodes == null) return;
                for (int i = 0; i < nodes.Length && del(nodes[i]); i++) { }
            }
        }

		/** Number of tiles along the X-axis */
		public int tileXCount;
        /** Number of tiles along the Z-axis */
        public int tileZCount;

        /** All tiles.
		 * A tile can be got from a tile coordinate as tiles[x + z*tileXCount]
		 */
        public FNavmeshTile[] tiles;

        // Larger worlds
        public const int VertexIndexMask = 0xFFF;

        public const int TileIndexMask = 0x7FFFF;
        public const int TileIndexOffset = 12;

		/** World bounds for the graph.
		 * Defined as a bounds object with size #forcedBoundsSize and centered at #forcedBoundsCenter
		 */
		public UnityEngine.Bounds forcedBounds
		{
			get
			{
				return new UnityEngine.Bounds(forcedBoundsCenter, forcedBoundsSize);
			}
		}

		/** Show an outline of the polygons in the Unity Editor */
		public bool showMeshOutline = true;

		/** Show the connections between the polygons in the Unity Editor */
		public bool showNodeConnections = false;

		/** Gets the vertex coordinate for the specified index.
		 *
		 * \throws IndexOutOfRangeException if the vertex index is invalid.
		 * \throws NullReferenceException if the tile the vertex is in is not calculated.
		 *
		 * \see NavmeshTile.GetVertex
		 */
		public Int3 GetVertex(int index)
		{
			int tileIndex = (index >> TileIndexOffset) & TileIndexMask;
			return tiles[tileIndex].GetVertex(index);
		}

		/** Returns a tile index from a vertex index */
		public int GetTileIndex(int index)
		{
			return (index >> TileIndexOffset) & TileIndexMask;
		}

		public int GetVertexArrayIndex(int index)
		{
			return index & VertexIndexMask;
		}

		/** Returns tile coordinates from a tile index */
		public void GetTileCoordinates(int tileIndex, out int x, out int z)
		{
			//z = System.Math.DivRem (tileIndex, tileXCount, out x);
			z = tileIndex / tileXCount;
			x = tileIndex - z * tileXCount;
		}

		/** Get all tiles.
		 * \warning Do not modify this array
		 */
		public FNavmeshTile[] GetTiles()
		{
			return tiles;
		}

		public void AddTile(int x, int z, FNavmeshTile navmesh)
		{
			var tileIndex = x + z * tileXCount;
			tiles[tileIndex] = navmesh;
		}

		public void RemoveTile(int x, int z)
		{
			var tileIndex = x + z * tileXCount;
			tiles[tileIndex] = null;
		}

        /** Returns an XZ bounds object with the bounds of a group of tiles.
		  * The bounds object is defined in world units.
		  */
        public UnityEngine.Bounds GetTileBounds(IntRect rect)
        {
            return GetTileBounds(rect.xmin, rect.ymin, rect.Width, rect.Height);
        }

        /** Returns an XZ bounds object with the bounds of a group of tiles.
		  * The bounds object is defined in world units.
		  */
        public UnityEngine.Bounds GetTileBounds(int x, int z, int width = 1, int depth = 1)
        {
            var b = new UnityEngine.Bounds();
            b.SetMinMax(
                new UnityEngine.Vector3(x * tileSizeX * cellSize, 0, z * tileSizeZ * cellSize) + forcedBounds.min,
                new UnityEngine.Vector3((x + width) * tileSizeX * cellSize, forcedBounds.size.y, (z + depth) * tileSizeZ * cellSize) + forcedBounds.min
            );
            return b;
        }

        /** Returns the tile coordinate which contains the point \a p.
		 * Is not necessarily a valid tile (i.e, it could be out of bounds).
		 */
        public Int2 GetTileCoordinates(UnityEngine.Vector3 p)
		{
			p -= forcedBounds.min;
			p.x /= cellSize * tileSizeX;
			p.z /= cellSize * tileSizeZ;
			return new Int2((int)p.x, (int)p.z);
		}

        /** Returns a rect containing the indices of all tiles touching the specified bounds */
        public IntRect GetTouchingTiles(UnityEngine.Bounds b)
        {
            b.center -= forcedBounds.min;

            //Calculate world bounds of all affected tiles
            var r = new IntRect(
				UnityEngine.Mathf.FloorToInt(b.min.x / (tileSizeX * cellSize)), 
				UnityEngine.Mathf.FloorToInt(b.min.z / (tileSizeZ * cellSize)), 
				UnityEngine.Mathf.FloorToInt(b.max.x / (tileSizeX * cellSize)), 
				UnityEngine.Mathf.FloorToInt(b.max.z / (tileSizeZ * cellSize)));
            //Clamp to bounds
            r = IntRect.Intersection(r, new IntRect(0, 0, tileXCount - 1, tileZCount - 1));
            return r;
        }

        /** Returns a rect containing the indices of all tiles by rounding the specified bounds to tile borders */
        public IntRect GetTouchingTilesRound(UnityEngine.Bounds b)
        {
            b.center -= forcedBounds.min;

			//Calculate world bounds of all affected tiles
			var r = new IntRect(
				UnityEngine.Mathf.RoundToInt(b.min.x / (tileSizeX * cellSize)),
				UnityEngine.Mathf.RoundToInt(b.min.z / (tileSizeZ * cellSize)),
				UnityEngine.Mathf.RoundToInt(b.max.x / (tileSizeX * cellSize)) - 1,
				UnityEngine.Mathf.RoundToInt(b.max.z / (tileSizeZ * cellSize)) - 1);
            //Clamp to bounds
            r = IntRect.Intersection(r, new IntRect(0, 0, tileXCount - 1, tileZCount - 1));
            return r;
        }

        public override void GetNodes(GraphNodeDelegateCancelable del)
		{
			/*if (nodes == null) return;
			for (int i=0;i<nodes.Length && del (nodes[i]);i++) {}*/
			if (tiles == null) return;
			//
			for (int i = 0; i < tiles.Length; i++)
			{
				if (tiles[i] == null || tiles[i].x + tiles[i].z * tileXCount != i) continue;
				TriangleMeshNode[] nodes = tiles[i].nodes;

				if (nodes == null) continue;

				for (int j = 0; j < nodes.Length && del(nodes[j]); j++) { }
			}
		}

		public override void OnDestroy()
		{

			base.OnDestroy();

			// Cleanup
			TriangleMeshNode.SetNavmeshHolder(FNavgationSystem.instance.GetGraphIndex(this), null);
		}

		public override void OnDrawGizmos(bool drawNodes)
		{

			if (!drawNodes)
			{
				return;
			}

			//if (bbTree != null)
			//{
			//	bbTree.OnDrawGizmos();
			//}

			UnityEngine.Gizmos.color = UnityEngine.Color.white;
			UnityEngine.Gizmos.DrawWireCube(forcedBounds.center, forcedBounds.size);

			//PathHandler debugData = AstarPath.active.debugPathData;

			GraphNodeDelegateCancelable del = delegate (GraphNode _node) {
				var node = _node as TriangleMeshNode;

				//if (AstarPath.active.showSearchTree && debugData != null)
				//{
				//	bool v = InSearchTree(node, AstarPath.active.debugPath);
				//	//debugData.GetPathNode(node).parent != null && debugData.GetPathNode(node).parent.node != null;
				//	if (v && showNodeConnections)
				//	{
				//		//Gizmos.color = new Color (0,1,0,0.7F);
				//		var pnode = debugData.GetPathNode(node);
				//		if (pnode.parent != null)
				//		{
				//			Gizmos.color = NodeColor(node, debugData);
				//			Gizmos.DrawLine((Vector3)node.position, (Vector3)debugData.GetPathNode(node).parent.node.position);
				//		}
				//	}

				//	if (showMeshOutline)
				//	{
				//		Gizmos.color = node.Walkable ? NodeColor(node, debugData) : AstarColor.UnwalkableNode;
				//		if (!v) Gizmos.color = Gizmos.color * new Color(1, 1, 1, 0.1f);

				//		Gizmos.DrawLine((Vector3)node.GetVertex(0), (Vector3)node.GetVertex(1));
				//		Gizmos.DrawLine((Vector3)node.GetVertex(1), (Vector3)node.GetVertex(2));
				//		Gizmos.DrawLine((Vector3)node.GetVertex(2), (Vector3)node.GetVertex(0));
				//	}
				//}
				//else
				{
					if (showNodeConnections)
					{
						UnityEngine.Gizmos.color = FDebugColor.NodeConnection;

						for (int q = 0; q < node.connections.Length; q++)
						{
							//Gizmos.color = Color.Lerp (Color.green,Color.red,node.connectionCosts[q]/8000F);
							UnityEngine.Gizmos.DrawSphere((UnityEngine.Vector3)node.position, 0.1f);
							UnityEngine.Gizmos.DrawSphere((UnityEngine.Vector3)node.connections[q].position, 0.1f);
							UnityEngine.Gizmos.DrawLine((UnityEngine.Vector3)node.position, UnityEngine.Vector3.Lerp((UnityEngine.Vector3)node.connections[q].position, (UnityEngine.Vector3)node.position, 0.4f));
						}
					}

					if (showMeshOutline)
					{
						UnityEngine.Gizmos.color = node.Walkable ? FDebugColor.MeshColor : FDebugColor.UnwalkableNode;


						UnityEngine.Gizmos.DrawLine((UnityEngine.Vector3)node.GetVertex(0), (UnityEngine.Vector3)node.GetVertex(1));
						UnityEngine.Gizmos.DrawLine((UnityEngine.Vector3)node.GetVertex(1), (UnityEngine.Vector3)node.GetVertex(2));
						UnityEngine.Gizmos.DrawLine((UnityEngine.Vector3)node.GetVertex(2), (UnityEngine.Vector3)node.GetVertex(0));
					}
				}

				//Gizmos.color.a = 0.2F;

				return true;
			};

			GetNodes(del);

			// draw vertex
			UnityEngine.Gizmos.color = UnityEngine.Color.blue;
			for (int tileIndex = 0; tileIndex < tiles.Length; ++tileIndex)
			{
				var tile = tiles[tileIndex];
				if (null == tile) continue;

				for (int vertexIndex = 0; vertexIndex < tile.verts.Length; ++vertexIndex)
				{
					var v = (UnityEngine.Vector3)tile.verts[vertexIndex];
					UnityEngine.Gizmos.DrawSphere(v, 0.1f);
				}
			}
		}
	}
}
