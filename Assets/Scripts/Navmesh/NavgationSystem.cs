
using Navmesh.Core;
using Navmesh.Nodes;
using System.Collections.Generic;

namespace Navmesh
{
	public class FDebugColor 
	{
		public static UnityEngine.Color NodeConnection = new UnityEngine.Color (1,1,1,0.9F);
		public static UnityEngine.Color UnwalkableNode = new UnityEngine.Color (1,0,0,0.5F);
		public static UnityEngine.Color BoundsHandles = new UnityEngine.Color (0.29F,0.454F,0.741F,0.9F);
		public static UnityEngine.Color ConnectionLowLerp = new UnityEngine.Color (0,1,0,0.5F);
		public static UnityEngine.Color ConnectionHighLerp = new UnityEngine.Color (1,0,0,0.5F);
		public static UnityEngine.Color MeshEdgeColor = new UnityEngine.Color (0,0,0,0.5F);
		public static UnityEngine.Color MeshColor = new UnityEngine.Color (0,0,0,0.5F);

		/** Returns bit number \a b from int \a a. The bit number is zero based. Relevant \a b values are from 0 to 31\n
		 * Equals to (a >> b) & 1
		 */
		static int Bit(int a, int b)
		{
			return (a >> b) & 1;
			//return (a & (1 << b)) >> b; //Original code, one extra shift operation required
		}

		/** Returns a nice color from int \a i with alpha \a a. Got code from the open-source Recast project, works really good\n
		 * Seems like there are only 64 possible colors from studying the code
		 */
		public static UnityEngine.Color IntToColor(int i, float a)
		{
			int r = Bit(i, 1) + Bit(i, 3) * 2 + 1;
			int g = Bit(i, 2) + Bit(i, 4) * 2 + 1;
			int b = Bit(i, 0) + Bit(i, 5) * 2 + 1;
			return new UnityEngine.Color(r * 0.25F, g * 0.25F, b * 0.25F, a);
		}
	}

	/** What data to draw the graph debugging with */
	public enum GraphDebugMode
	{
		Areas,
		G,
		H,
		F,
		Penalty,
		Connections,
		Tags
	}

	public class FNavgationSystem
    {
        public static FNavgationSystem instance = null;

        public FNavGraph[] graphs = new FNavGraph[0];

		#region InspectorDebug
		/** @name Inspector - Debug
		 * @{ */

		/** Toggle for showing the gizmo debugging for the graphs in the scene view (editor only). */
		public bool showNavGraphs = true;

		/** Toggle to show unwalkable nodes.
		 *
		 * \note Only relevant in the editor
		 *
		 * \see unwalkableNodeDebugSize
		 */
		public bool showUnwalkableNodes = true;

		/** The mode to use for drawing nodes in the sceneview.
		 *
		 * \note Only relevant in the editor
		 *
		 * \see Pathfinding.GraphDebugMode
		 */
		public GraphDebugMode debugMode;

		/** Low value to use for certain #debugMode modes.
		 * For example if #debugMode is set to G, this value will determine when the node will be totally red.
		 *
		 * \note Only relevant in the editor
		 * \see #debugRoof
		 */
		public float debugFloor = 0;

		/** High value to use for certain #debugMode modes.
		 * For example if #debugMode is set to G, this value will determine when the node will be totally green.
		 *
		 * For the penalty debug mode, the nodes will be colored green when they have a penalty of zero and red
		 * when their penalty is greater or equal to this value and something between red and green for values in between.
		 *
		 * \note Only relevant in the editor
		 *
		 * \see #debugFloor

		 */
		public float debugRoof = 20000;
		#endregion

		public void Awake()
		{
			instance = this;
		}

		/** Adds the specified graph to the #graphs array */
		public void AddGraph(FNavGraph graph)
		{

			// Make sure to not interfere with pathfinding
			// AstarPath.active.BlockUntilPathQueueBlocked();

			//Try to fill in an empty position
			for (int i = 0; i < graphs.Length; i++)
			{
				if (graphs[i] == null)
				{
					graphs[i] = graph;
					graph.Awake();
					graph.graphIndex = (uint)i;
					return;
				}
			}

			if (graphs != null && graphs.Length >= GraphNode.MaxGraphIndex)
			{
				throw new System.Exception("Graph Count Limit Reached. You cannot have more than " + GraphNode.MaxGraphIndex +
					" graphs. Some compiler directives can change this limit, e.g ASTAR_MORE_AREAS, look under the " +
					"'Optimizations' tab in the A* Inspector");
			}

			//Add a new entry to the list
			var ls = new List<FNavGraph>(graphs);
			ls.Add(graph);
			graphs = ls.ToArray();

			graph.Awake();
			graph.graphIndex = (uint)(graphs.Length - 1);
		}

		/** Removes the specified graph from the #graphs array and Destroys it in a safe manner.
		 * To avoid changing graph indices for the other graphs, the graph is simply nulled in the array instead
		 * of actually removing it from the array.
		 * The empty position will be reused if a new graph is added.
		 * 
		 * \returns True if the graph was sucessfully removed (i.e it did exist in the #graphs array). False otherwise.
		 * 
		 * 
		 * \version Changed in 3.2.5 to call SafeOnDestroy before removing
		 * and nulling it in the array instead of removing the element completely in the #graphs array.
		 * 
		 */
		public bool RemoveGraph(FNavGraph graph)
		{

			// Make sure all graph updates and other callbacks are done
			// active.FlushWorkItems(false, true);

			// Make sure the pathfinding threads are stopped
			// active.BlockUntilPathQueueBlocked();

			// //Safe OnDestroy is called since there is a risk that the pathfinding is searching through the graph right now,
			// //and if we don't wait until the search has completed we could end up with evil NullReferenceExceptions
			graph.OnDestroy();

			int i = System.Array.IndexOf(graphs, graph);

			if (i == -1)
			{
				return false;
			}

			graphs[i] = null;

			return true;
		}

		/** Gets the index of the NavGraph in the #graphs array */
		public int GetGraphIndex(FNavGraph graph)
		{
			if (graph == null) throw new System.ArgumentNullException("graph");

			if (graphs != null)
			{
				for (int i = 0; i < graphs.Length; i++)
				{
					if (graph == graphs[i])
					{
						return i;
					}
				}
			}
			UnityEngine.Debug.LogError("Graph doesn't exist");
			return -1;
		}

		/* Color to use for gizmos.
		 * Returns a color to be used for the specified node with the current debug settings (editor only).
		 *
		 * \version Since 3.6.1 this method will not handle null nodes
		 */
		public virtual UnityEngine.Color NodeColor(GraphNode node)
		{
			UnityEngine.Color c = FDebugColor.NodeConnection;

			switch (debugMode)
			{
				case GraphDebugMode.Penalty:
					c = UnityEngine.Color.Lerp(FDebugColor.ConnectionLowLerp, FDebugColor.ConnectionHighLerp, ((float)node.Penalty - debugFloor) / (debugRoof - debugFloor));
					break;
				case GraphDebugMode.Tags:
					c = FDebugColor.IntToColor((int)node.Tag, 0.5F);
					break;
				case GraphDebugMode.Connections:
					c = FDebugColor.NodeConnection;
					break;
				default:
					return FDebugColor.MeshEdgeColor;

					//PathNode nodeR = data.GetPathNode(node);

					//switch (AstarPath.active.debugMode)
					//{
					//	case GraphDebugMode.G:
					//		c = Color.Lerp(AstarColor.ConnectionLowLerp, AstarColor.ConnectionHighLerp, ((float)nodeR.G - AstarPath.active.debugFloor) / (AstarPath.active.debugRoof - AstarPath.active.debugFloor));
					//		break;
					//	case GraphDebugMode.H:
					//		c = Color.Lerp(AstarColor.ConnectionLowLerp, AstarColor.ConnectionHighLerp, ((float)nodeR.H - AstarPath.active.debugFloor) / (AstarPath.active.debugRoof - AstarPath.active.debugFloor));
					//		break;
					//	case GraphDebugMode.F:
					//		c = Color.Lerp(AstarColor.ConnectionLowLerp, AstarColor.ConnectionHighLerp, ((float)nodeR.F - AstarPath.active.debugFloor) / (AstarPath.active.debugRoof - AstarPath.active.debugFloor));
					//		break;
					//}
					//break;
			}

			c.a *= 0.5F;
			return c;
		}
	}
}
