using Navmesh.Core;
using Navmesh.Serialization;

namespace Navmesh.Nodes
{
	public interface INavmeshHolder
	{
		Int3 GetVertex(int i);
		int GetVertexArrayIndex(int index);
		void GetTileCoordinates(int tileIndex, out int x, out int z);
	}

	public class TriangleMeshNode : MeshNode
    {
		// public TriangleMeshNode(AstarPath astar) : base(astar) { }

		/** Internal vertex index for the first vertex */
		public int v0;

		/** Internal vertex index for the second vertex */
		public int v1;

		/** Internal vertex index for the third vertex */
		public int v2;

		protected static INavmeshHolder[] _navmeshHolders = new INavmeshHolder[0];
		public static INavmeshHolder GetNavmeshHolder(uint graphIndex)
		{
			return _navmeshHolders[(int)graphIndex];
		}

		/** Sets the internal navmesh holder for a given graph index.
		 * \warning Internal method
		 */
		public static void SetNavmeshHolder(int graphIndex, INavmeshHolder graph)
		{
			if (_navmeshHolders.Length <= graphIndex)
			{
				var gg = new INavmeshHolder[graphIndex + 1];
				for (int i = 0; i < _navmeshHolders.Length; i++) gg[i] = _navmeshHolders[i];
				_navmeshHolders = gg;
			}
			_navmeshHolders[graphIndex] = graph;
		}

		/** Set the position of this node to the average of its 3 vertices */
		public void UpdatePositionFromVertices()
		{
			INavmeshHolder g = GetNavmeshHolder(GraphIndex);
			position = (g.GetVertex(v0) + g.GetVertex(v1) + g.GetVertex(v2)) * 0.333333f;
		}

		/** Return a number identifying a vertex.
		 * This number does not necessarily need to be a index in an array but two different vertices (in the same graph) should
		 * not have the same vertex numbers.
		 */
		public int GetVertexIndex(int i)
		{
			return i == 0 ? v0 : (i == 1 ? v1 : v2);
		}

		/** Return a number specifying an index in the source vertex array.
		 * The vertex array can for example be contained in a recast tile, or be a navmesh graph, that is graph dependant.
		 * This is slower than GetVertexIndex, if you only need to compare vertices, use GetVertexIndex.
		 */
		public int GetVertexArrayIndex(int i)
		{
			return GetNavmeshHolder(GraphIndex).GetVertexArrayIndex(i == 0 ? v0 : (i == 1 ? v1 : v2));
		}

		public override Int3 GetVertex(int i)
		{
			return GetNavmeshHolder(GraphIndex).GetVertex(GetVertexIndex(i));
		}

		public override int GetVertexCount()
		{
			// A triangle has 3 vertices
			return 3;
		}

		public override UnityEngine.Vector3 ClosestPointOnNode(UnityEngine.Vector3 p)
		{
			return UnityEngine.Vector3.zero;
		}
		public override UnityEngine.Vector3 ClosestPointOnNodeXZ(UnityEngine.Vector3 p)
		{
			return UnityEngine.Vector3.zero;
		}

		public override bool GetPortal(GraphNode _other, System.Collections.Generic.List<UnityEngine.Vector3> left, System.Collections.Generic.List<UnityEngine.Vector3> right, bool backwards)
		{
			int aIndex, bIndex;
			return GetPortal(_other, left, right, backwards, out aIndex, out bIndex);
		}

		public bool GetPortal(GraphNode _other, System.Collections.Generic.List<UnityEngine.Vector3> left, System.Collections.Generic.List<UnityEngine.Vector3> right, bool backwards, out int aIndex, out int bIndex)
		{
			aIndex = -1;
			bIndex = -1;

			//If the nodes are in different graphs, this function has no idea on how to find a shared edge.
			if (_other.GraphIndex != GraphIndex) return false;

			// Since the nodes are in the same graph, they are both TriangleMeshNodes
			// So we don't need to care about other types of nodes
			var other = _other as TriangleMeshNode;

			//Get tile indices
			int tileIndex = (GetVertexIndex(0) >> FTiledNavmeshGraph.TileIndexOffset) & FTiledNavmeshGraph.TileIndexMask;
			int tileIndex2 = (other.GetVertexIndex(0) >> FTiledNavmeshGraph.TileIndexOffset) & FTiledNavmeshGraph.TileIndexMask;

			//When the nodes are in different tiles, the edges might not be completely identical
			//so another technique is needed
			//Only do this on recast graphs
			if (tileIndex != tileIndex2)
			{
				//Get the tile coordinates, from them we can figure out which edge is going to be shared
				int x1, x2, z1, z2;
				int coord;
				INavmeshHolder nm = GetNavmeshHolder(GraphIndex);
				nm.GetTileCoordinates(tileIndex, out x1, out z1);
				nm.GetTileCoordinates(tileIndex2, out x2, out z2);

				if (System.Math.Abs(x1 - x2) == 1) coord = 0;
				else if (System.Math.Abs(z1 - z2) == 1) coord = 2;
				else throw new System.Exception("Tiles not adjacent (" + x1 + ", " + z1 + ") (" + x2 + ", " + z2 + ")");

				int av = GetVertexCount();
				int bv = other.GetVertexCount();

				//Try the X and Z coordinate. For one of them the coordinates should be equal for one of the two nodes' edges
				//The midpoint between the tiles is the only place where they will be equal

				int first = -1, second = -1;

				//Find the shared edge
				for (int a = 0; a < av; a++)
				{
					int va = GetVertex(a)[coord];
					for (int b = 0; b < bv; b++)
					{
						if (va == other.GetVertex((b + 1) % bv)[coord] && GetVertex((a + 1) % av)[coord] == other.GetVertex(b)[coord])
						{
							first = a;
							second = b;
							a = av;
							break;
						}
					}
				}

				aIndex = first;
				bIndex = second;

				if (first != -1)
				{

					Int3 a = GetVertex(first);
					Int3 b = GetVertex((first + 1) % av);

					//The coordinate which is not the same for the vertices
					int ocoord = coord == 2 ? 0 : 2;

					//When the nodes are in different tiles, they might not share exactly the same edge
					//so we clamp the portal to the segment of the edges which they both have.
					int mincoord = System.Math.Min(a[ocoord], b[ocoord]);
					int maxcoord = System.Math.Max(a[ocoord], b[ocoord]);

					mincoord = System.Math.Max(mincoord, System.Math.Min(other.GetVertex(second)[ocoord], other.GetVertex((second + 1) % bv)[ocoord]));
					maxcoord = System.Math.Min(maxcoord, System.Math.Max(other.GetVertex(second)[ocoord], other.GetVertex((second + 1) % bv)[ocoord]));

					if (a[ocoord] < b[ocoord])
					{
						a[ocoord] = mincoord;
						b[ocoord] = maxcoord;
					}
					else
					{
						a[ocoord] = maxcoord;
						b[ocoord] = mincoord;
					}

					if (left != null)
					{
						//All triangles should be clockwise so second is the rightmost vertex (seen from this node)
						left.Add((UnityEngine.Vector3)a);
						right.Add((UnityEngine.Vector3)b);
					}
					return true;
				}
			}
			else if (!backwards)
			{

				int first = -1;
				int second = -1;

				int av = GetVertexCount();
				int bv = other.GetVertexCount();

				/** \todo Maybe optimize with pa=av-1 instead of modulus... */
				for (int a = 0; a < av; a++)
				{
					int va = GetVertexIndex(a);
					for (int b = 0; b < bv; b++)
					{
						if (va == other.GetVertexIndex((b + 1) % bv) && GetVertexIndex((a + 1) % av) == other.GetVertexIndex(b))
						{
							first = a;
							second = b;
							a = av;
							break;
						}

					}
				}

				aIndex = first;
				bIndex = second;

				if (first != -1)
				{

					if (left != null)
					{
						//All triangles should be clockwise so second is the rightmost vertex (seen from this node)
						left.Add((UnityEngine.Vector3)GetVertex(first));
						right.Add((UnityEngine.Vector3)GetVertex((first + 1) % av));
					}
				}
				else
				{
					return false;
				}
			}

			return true;
		}

		public override void SerializeNode(GraphSerializationContext ctx)
		{
			base.SerializeNode(ctx);
			//ctx.writer.Write(v0);
			//ctx.writer.Write(v1);
			//ctx.writer.Write(v2);
		}

		public override void DeserializeNode(GraphSerializationContext ctx)
		{
			base.DeserializeNode(ctx);
			//v0 = ctx.reader.ReadInt32();
			//v1 = ctx.reader.ReadInt32();
			//v2 = ctx.reader.ReadInt32();
		}
	}
}
