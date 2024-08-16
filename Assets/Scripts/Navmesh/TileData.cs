
using Microsoft.SqlServer.Server;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Reflection;
using System.Security.Cryptography;
using UnityEditor.PackageManager;
using UnityEngine;
using static UnityEngine.Rendering.DebugUI;

namespace Navmesh
{
    public class FConvexShape
    {
        public enum eSide
        {
            SIDE_FRONT = 0,
            SIDE_BACK = 1,
            SIDE_ON = 2,
            SIDE_CROSS = 3
        };

        public FConvexShape(UnityEngine.Vector3[] InVertices)
        {
            Vertices.AddRange(InVertices);
        }

        public FConvexShape(List<UnityEngine.Vector3> InVertices)
        {
            Vertices.AddRange(InVertices);
        }

        public int NumPoints { get { return Vertices.Count; } }
        public int NumEdges { get { return Vertices.Count; } }
        public UnityEngine.Vector3 this[int index]
        {
            get
            {
                return Vertices[index];
            }
        }

        public List<UnityEngine.Vector3> Vertices = new List<UnityEngine.Vector3>();

        public UnityEngine.Vector3 GetCenter()
        {
            UnityEngine.Vector3 Center = UnityEngine.Vector3.zero;
            int numPoints = NumPoints;

            if (numPoints < 3) return Center;

            for (int i = 0; i < numPoints; i++)
            {
                Center += Vertices[i];
            }
            Center *= (1.0f / numPoints);
            return Center;
        }

        public bool GetFacePlane(out UnityEngine.Vector3 OutPoint, out UnityEngine.Vector3 OutNormal)
        {
	        int numPoints = NumPoints;

	        if (numPoints < 3) {
                OutPoint = new UnityEngine.Vector3();
                OutNormal = new UnityEngine.Vector3();
		        return false;
	        }

	        var Center = GetCenter();
	        var v1 = Vertices[0] - Center;
	        var v2 = Vertices[1] -  Center;
            var normal = UnityEngine.Vector3.Cross(v2, v1);
            OutPoint = v1;
            OutNormal = normal.normalized;
	        return true;
        }

        public bool GetEdgePlane(int InEdgeIndex, out UnityEngine.Vector3 OutPoint, out UnityEngine.Vector3 OutNormal)
        {
            OutPoint = new UnityEngine.Vector3();
            OutNormal = new UnityEngine.Vector3();

            if (InEdgeIndex < 0 || InEdgeIndex >= NumPoints)
            {
                return false;
            }

            var V0 = Vertices[InEdgeIndex];
            var V1 = Vertices[(InEdgeIndex + 1) % NumPoints];
            var Dir = V1 - V0;
            var N = UnityEngine.Vector3.Cross(UnityEngine.Vector3.up, Dir);

            OutPoint = V0; 
            OutNormal = N.normalized;
            return true;
        }

        public bool GetEdge(int InEdgeIndex, out UnityEngine.Vector3 OutPoint0, out UnityEngine.Vector3 OutPoint1, out UnityEngine.Vector3 OutNormal, out float OutLength)
        {
            if (InEdgeIndex < 0 || InEdgeIndex >= NumEdges)
            {
                OutPoint0 = UnityEngine.Vector3.zero;
                OutPoint1 = UnityEngine.Vector3.zero;
                OutNormal = UnityEngine.Vector3.zero;
                OutLength = 0;
                return false;
            }

            OutPoint0 = Vertices[InEdgeIndex];
            OutPoint1 = Vertices[(InEdgeIndex+1)% NumEdges];
            var Dir = OutPoint1 - OutPoint0;
            OutLength = Dir.magnitude;
            OutNormal = OutLength > 0? Dir / OutLength : UnityEngine.Vector3.zero;
            return true;
        }

        // Plane将本Convex裁剪为front和back，且保留本Convex数据不变
        public eSide Split(
            UnityEngine.Vector3 InClipPoint, 
            UnityEngine.Vector3 InClipNormal,
            out FConvexShape OutFrontConvex,
            out FConvexShape OutBackConvex)
        {
            OutFrontConvex = null;
            OutBackConvex = null;

            var Dists = new float[NumPoints + 4];
            var Sides = new int[NumPoints + 4];

            var Counts = new int[3] { 0, 0, 0 };

            var W = UnityEngine.Vector3.Dot(InClipPoint, InClipNormal);
            // determine sides for each point
            int i = 0;
            for (i = 0; i < NumPoints; i++)
            {
                var V = Vertices[i];
                var Dot = UnityEngine.Vector3.Dot(V, InClipNormal) - W;
                if (Dot > UnityEngine.Mathf.Epsilon)
                {
                    Sides[i] = (int)eSide.SIDE_FRONT;
                }
                else if (Dot < -UnityEngine.Mathf.Epsilon)
                {
                    Sides[i] = (int)eSide.SIDE_BACK;
                }
                else
                {
                    Sides[i] = (int)eSide.SIDE_ON;
                }
                Dists[i] = Dot;
                Counts[Sides[i]]++;
            }
            Sides[i] = Sides[0];
            Dists[i] = Dists[0];

            // if coplanar, put on the front side if the normals match
            if (Counts[(int)eSide.SIDE_FRONT] == 0 && Counts[(int)eSide.SIDE_BACK] == 0)
            {
                if (GetFacePlane(out var OutPoint, out var OutNormal))
                {
                    var d = UnityEngine.Vector3.Dot(OutNormal, InClipNormal);
                    if (d > 0.0f)
                    {
                        OutFrontConvex = Clone();
                        return eSide.SIDE_FRONT;
                    }
                    else
                    {
                        OutBackConvex = Clone();
                        return eSide.SIDE_BACK;
                    }
                }
                return eSide.SIDE_FRONT;
            }
            // if nothing at the front of the clipping plane
            if (Counts[(int)eSide.SIDE_FRONT] == 0)
            {
                OutBackConvex = Clone();
                return eSide.SIDE_BACK;
            }
            // if nothing at the back of the clipping plane
            if (Counts[(int)eSide.SIDE_BACK] == 0)
            {
                OutFrontConvex = Clone();
                return eSide.SIDE_FRONT;
            }
            var maxpts = NumPoints + 4; // cant use counts[0]+2 because of fp grouping errors

            var f = new List<UnityEngine.Vector3>();
            var b = new List<UnityEngine.Vector3>();

            for (i = 0; i < NumPoints; i++)
            {
                var p1 = Vertices[i];

                if (Sides[i] == (int)eSide.SIDE_ON)
                {
                    f.Add(p1);
                    b.Add(p1);
                    continue;
                }

                if (Sides[i] == (int)eSide.SIDE_FRONT)
                {
                    f.Add(p1);
                }

                if (Sides[i] == (int)eSide.SIDE_BACK)
                {
                    b.Add(p1);
                }

                if (Sides[i + 1] == (int)eSide.SIDE_ON || Sides[i + 1] == Sides[i])
                {
                    continue;
                }

                // generate a split point
                var p2 = Vertices[(i + 1) % NumPoints];

                // always calculate the split going from the same side
                // or minor epsilon issues can happen
                var mid = new UnityEngine.Vector3();
                if (Sides[i] == (int)eSide.SIDE_FRONT)
                {
                    var dot = Dists[i] / (Dists[i] - Dists[i + 1]);
                    for (int j = 0; j < 3; j++)
                    {
                        // avoid round off error when possible
                        if (InClipNormal[j] == 1.0f)
                        {
                            mid[j] = W;
                        }
                        else if (InClipNormal[j] == -1.0f)
                        {
                            mid[j] = -W;
                        }
                        else
                        {
                            mid[j] = p1[j] + dot * (p2[j] - p1[j]);
                        }
                    }
                }
                else
                {
                    var dot = Dists[i + 1] / (Dists[i + 1] - Dists[i]);
                    for (int j = 0; j < 3; j++)
                    {
                        // avoid round off error when possible
                        if (InClipNormal[j] == 1.0f)
                        {
                            mid[j] = W;
                        }
                        else if (InClipNormal[j] == -1.0f)
                        {
                            mid[j] = -W;
                        }
                        else
                        {
                            mid[j] = p2[j] + dot * (p1[j] - p2[j]);
                        }
                    }
                }

                f.Add(mid);
                b.Add(mid);
            }

            if (f.Count > maxpts || b.Count > maxpts)
            {
                UnityEngine.Debug.LogError("FConvexShape::Split: points exceeded estimate.");
            }

            OutFrontConvex = new FConvexShape(f);
            OutBackConvex = new FConvexShape(b);
            return eSide.SIDE_CROSS;
        }

        public int InsertPointAfter(int InIndex, UnityEngine.Vector3 InPoint)
        {
            if (InIndex == Vertices.Count - 1)
            {
                Vertices.Add(InPoint);
            }
            else
            {
                Vertices.Insert(InIndex + 1, InPoint);
            }
            return InIndex + 1;
        }

        public FConvexShape Clone()
        {
            return  new FConvexShape(new List<UnityEngine.Vector3>(Vertices));
        }

        public void ExtractPoints2D(List<List<UnityEngine.Vector2>> outputPolygons)
        {
            var points = new List<UnityEngine.Vector2>();
            for (int i = 0; i < Vertices.Count; i++) 
            {
                points.Add(new UnityEngine.Vector2(Vertices[i][0], Vertices[i][2]));
            }
            outputPolygons.Add(points);
        }

        public void DrawGizmos(Color c)
        {
            var center = GetCenter();
            center.y = 0;

            float scale = 0.95f;

            for (int i = 0, ni= NumPoints; i < ni;i++)
            {
                Gizmos.color = c;

                int ii = (i+1)%ni;
                var v0 = Vertices[i]; v0.y = 0.0f;
                var v1 = Vertices[ii]; v1.y = 0.0f;

                v0 = center + (v0 - center) * scale;
                v1 = center + (v1 - center) * scale;
                Gizmos.DrawLine(v0, v1);

                if (GetEdgePlane(i, out var p, out var n))
                {
                    Gizmos.DrawLine(v0, v0 + n * 0.3f);
                }

                if (i == 0) 
                {
                    Gizmos.color = Color.red;
                    Gizmos.DrawCube(v0, UnityEngine.Vector3.one * 0.2f);
                }
                else if (i == 1)
                {
                    Gizmos.color = Color.blue;
                    Gizmos.DrawCube(v0, UnityEngine.Vector3.one * 0.2f);
                }
                else
                {
                    Gizmos.color = Color.green;
                    Gizmos.DrawCube(v0, UnityEngine.Vector3.one * 0.2f);
                }
            }
        }
    }

    public class FTileData
    { 
        public int TileX { get; private set; }
        public int TileZ { get; private set; }
        public UnityEngine.Vector3 MinBounds { get; private set; }
        public UnityEngine.Vector3 MaxBounds { get; private set; }

        public Game.Utils.Triangulation.DelaunayTriangulation Triangulation { get; private set; }

        public List<FConvexShape> ConvexShapes { get; private set; }

        public struct FInitTileDataParams
        {
            public int TileX;
            public int TileZ;
            public UnityEngine.Vector3 MinBounds;
            public UnityEngine.Vector3 MaxBounds;
        }
        public bool Initialize(FInitTileDataParams InParams)
        {
            TileX = InParams.TileX; 
            TileZ = InParams.TileZ;
            MinBounds = InParams.MinBounds;
            MaxBounds = InParams.MaxBounds;

            ConvexShapes = new List<FConvexShape>();
            // Triangulation = new Game.Utils.Triangulation.DelaunayTriangulation();

            return true;
        }

        public bool AddConvexShape(FConvexShape InConvexShape)
        {
            // 1.用Tile的Bounds裁剪Shape
            //
            //   :''''''''':
            //   : +-----+ :
            //   : |     | :
            //   : |     |<--- tile to build
            //   : |     | :  
            //   : +-----+ :<-- geometry needed
            //   :.........:
            UnityEngine.Vector3[] ClipPlanes = new UnityEngine.Vector3[]
            {
                new UnityEngine.Vector3(MinBounds[0], MinBounds[1], MinBounds[2]), new UnityEngine.Vector3(-1.0f, 0.0f, 0.0f), // Point + Normal
                new UnityEngine.Vector3(MaxBounds[0], MinBounds[1], MinBounds[2]), new UnityEngine.Vector3(0.0f, 0.0f, -1.0f),
                new UnityEngine.Vector3(MaxBounds[0], MinBounds[1], MaxBounds[2]), new UnityEngine.Vector3(1.0f, 0.0f, 0.0f),
                new UnityEngine.Vector3(MinBounds[0], MinBounds[1], MaxBounds[2]), new UnityEngine.Vector3(0.0f, 0.0f, 1.0f),
            };

            var CurrConvexShape = InConvexShape;
            for (int i = 0; i < 4 && CurrConvexShape != null; ++i)
            {
                var Point = ClipPlanes[i * 2];
                var Normal = ClipPlanes[i * 2 + 1];

                var Side = CurrConvexShape.Split(Point, Normal, out var OutFront, out var OutBack);
                CurrConvexShape = OutBack;
            }

            // Shape和Tile不相交
            if (CurrConvexShape == null) return false;

            // 2.添加约Shape，需要继续和已有的Convex做裁剪，因为Tirangulation算法要求Convex之间不能重叠
            List<FConvexShape> Fronts = new List<FConvexShape>();
            for (int i=0; i<ConvexShapes.Count; ++i)
            {
                var ClippedConvex = ConvexShapes[i];
                var NumEdges = CurrConvexShape.NumEdges;
                for (int j=0; j<NumEdges && ClippedConvex != null; ++j)
                {
                    if (CurrConvexShape.GetEdgePlane(j, out var P, out var N))
                    {
                        var Side = ClippedConvex.Split(P, N, out var OutFront, out var OutBack);
                        if (OutFront != null) Fronts.Add(OutFront);
                        ClippedConvex = OutBack;
                    }
                }
            }
            ConvexShapes.Clear();
            ConvexShapes.AddRange(Fronts);
            ConvexShapes.Add(CurrConvexShape);
            return true; 
        }

        struct FSharedEdge
        {
            public int convexAIndex;
            public int convexAEdgeIndex;
            public int convexBIndex;
            public int convexBEdgeIndex;
        }
        public bool Triangulate()
        {
            // 1.构造Bounds点
            List<UnityEngine.Vector2> pointsToTriangulate = new List<UnityEngine.Vector2>
            {
                new UnityEngine.Vector2(MinBounds[0], MinBounds[2]),
                new UnityEngine.Vector2(MaxBounds[0], MinBounds[2]),
                new UnityEngine.Vector2(MaxBounds[0], MaxBounds[2]),
                new UnityEngine.Vector2(MinBounds[0], MaxBounds[2])
            };

            int numConvex = Mathf.Min(3, ConvexShapes.Count); // 测试使用
            // 2.合并所有的convex, 构造无重叠edge的hole(因为delaunay triangulation不支持edge重叠情况)
            // 2.1.找到2个convex的邻接edge(经过上面的裁剪流程，确保只会有1条邻接edge)
            var convexSharedEdgeIndex = new int[numConvex][];
            for (int i=0; i<numConvex; ++i)
            {
                var convex = ConvexShapes[i];
                var maxNumEdge = convex.NumEdges * 2;   // 因为会新增edge，这里预估一个最大的
                convexSharedEdgeIndex[i] = new int[maxNumEdge];
                for (var t = 0; t < maxNumEdge; ++t) convexSharedEdgeIndex[i][t] = -1;
            }

            System.Func<float, float, float, bool> isNearlyEqual = (a, b, t) => { 
                return Mathf.Abs(a-b) < t;
            };
            var float_cmp_tolerance = 0.0001f;

            var sharedEdges = new List<FSharedEdge>();
            for (var i = 0; i < numConvex; ++i)
            {
                var convexA = ConvexShapes[i];
                var numEdgeA = convexA.NumEdges;
                for (var j = i + 1; j < numConvex; ++j)
                {
                    var is_merged = false;
                    var convexB = ConvexShapes[j];
                    var numEdgeB = convexB.NumEdges;
                    for (var edgeIndexA = 0; edgeIndexA < numEdgeA && !is_merged; ++edgeIndexA)
                    {
                        if (!convexA.GetEdge(edgeIndexA, out var pA0, out var pA1, out var dirA, out var lenA)) continue;

                        for (var edgeIndexB = 0; edgeIndexB < numEdgeB && !is_merged; ++edgeIndexB)
                        {
                            if (!convexB.GetEdge(edgeIndexB, out var pB0, out var pB1, out var dirB, out var lenB)) continue;

                            var v = new UnityEngine.Vector3[] {
                                    pB0 - pA0,
                                    pB1 - pA0,
                                };

                            var pB = new UnityEngine.Vector3[] { pB0, pB1 };
                            var proj_dist = new float[2];
                            var dist = new float[2];
                            var is_collinear = true;
                            // 检查距离
                            for (int k = 0; k < 2; ++k)
                            {
                                proj_dist[k] = UnityEngine.Vector3.Dot(v[k], dirA);
                                dist[k] = (v[k] - proj_dist[k] * dirA).magnitude;
                                if (!isNearlyEqual(dist[k], 0.0f, float_cmp_tolerance))
                                {
                                    is_collinear = false;
                                    break;
                                }
                            }
                            if (!is_collinear) continue;

                            // 检查位置关系
                            if (proj_dist[0] < float_cmp_tolerance && proj_dist[1] < float_cmp_tolerance) continue;
                            if (proj_dist[0] > (lenA - float_cmp_tolerance) && proj_dist[1] > (lenA - float_cmp_tolerance)) continue;

                            var min_index = proj_dist[0] < proj_dist[1] ? 0 : 1;
                            var max_index = (min_index + 1) % 2;
                            var is_same_pA0 = isNearlyEqual(proj_dist[min_index], 0.0f, float_cmp_tolerance);
                            var is_same_pA1 = isNearlyEqual(proj_dist[max_index], lenA, float_cmp_tolerance);

                            if (proj_dist[min_index] <= float_cmp_tolerance)
                            {
                                // proj_dist[max_index] must > float_cmp_tolerance
                                if (proj_dist[max_index] <= (lenA + float_cmp_tolerance))
                                {
                                    // pB_min, pA0, pB_max, pA1
                                    // ConvexA shared edge pA0 -> pB_max
                                    // ConvexB shared edge pA0 -> pB_max or pB_max -> pA0
                                    var new_edgeA = is_same_pA1? edgeIndexA : convexA.InsertPointAfter(edgeIndexA, pB[max_index]);
                                    var new_edgeB = is_same_pA0? edgeIndexB : convexB.InsertPointAfter(edgeIndexB, pA0);
                                    sharedEdges.Add(new FSharedEdge() { 
                                        convexAIndex = i, convexAEdgeIndex = edgeIndexA,
                                        convexBIndex = j, convexBEdgeIndex = min_index < max_index? new_edgeB : edgeIndexB,
                                    });
                                }
                                else
                                {
                                    // pB_min, pA0, pA1, pB_max
                                    // ConvexA shared edge pA0 -> pA1
                                    // ConvexB shared edge pA0 -> pA1 or pA1 -> pA0
                                    int new_edgeB0 = -1, new_edgeB1 = -1;
                                    if (min_index < max_index) 
                                    {
                                        new_edgeB0 = is_same_pA0? edgeIndexB : convexB.InsertPointAfter(edgeIndexB, pA0);
                                        new_edgeB1 = is_same_pA1? new_edgeB0 : convexB.InsertPointAfter(new_edgeB0, pA1);
                                    }
                                    else
                                    {
                                        new_edgeB0 = is_same_pA1? edgeIndexB : convexB.InsertPointAfter(edgeIndexB, pA1);
                                        new_edgeB1 = is_same_pA0? new_edgeB0 : convexB.InsertPointAfter(new_edgeB0, pA0);
                                    }
                                    sharedEdges.Add(new FSharedEdge()
                                    {
                                        convexAIndex = i,
                                        convexAEdgeIndex = edgeIndexA,
                                        convexBIndex = j,
                                        convexBEdgeIndex = new_edgeB0,
                                    });
                                }
                            }
                            else
                            {
                                if (proj_dist[max_index] <= (lenA + float_cmp_tolerance))
                                {
                                    // pA0, pB_min, pB_max, pA1
                                    // ConvexA shared edge pB_min -> pB_max
                                    // ConvexB shared edge pB_min -> pB_max
                                    var new_edgeA0 = is_same_pA0? edgeIndexA : convexA.InsertPointAfter(edgeIndexA, pB[min_index]);
                                    var new_edgeA1 = is_same_pA1? new_edgeA0 : convexA.InsertPointAfter(new_edgeA0, pB[max_index]);
                                    sharedEdges.Add(new FSharedEdge()
                                    {
                                        convexAIndex = i,
                                        convexAEdgeIndex = new_edgeA0,
                                        convexBIndex = j,
                                        convexBEdgeIndex = edgeIndexB,
                                    });
                                }
                                else
                                {
                                    // pA0, pB_min, pA1, pB_max
                                    // ConvexA shared edge pB_min -> pA1
                                    // ConvexB shared edge pB_min -> pA1 or pA1 -> pB_min
                                    var new_edgeA0 = is_same_pA0? edgeIndexA : convexA.InsertPointAfter(edgeIndexA, pB[min_index]);
                                    var new_edgeB0 = is_same_pA1? edgeIndexB : convexB.InsertPointAfter(edgeIndexB, pA1);
                                    sharedEdges.Add(new FSharedEdge()
                                    {
                                        convexAIndex = i,
                                        convexAEdgeIndex = new_edgeA0,
                                        convexBIndex = j,
                                        convexBEdgeIndex = new_edgeB0,
                                    });
                                }
                            }
                            // 标记convex共享边信息
                            var sharedEdgeIndex = sharedEdges.Count - 1;
                            var sharedEdge = sharedEdges[sharedEdgeIndex];
                            convexSharedEdgeIndex[i][sharedEdge.convexAEdgeIndex] = sharedEdgeIndex;
                            convexSharedEdgeIndex[j][sharedEdge.convexBEdgeIndex] = sharedEdgeIndex;
                            is_merged = true;
                        }
                    }
                }
            }

            m_sharedEdges = sharedEdges;

            List<List<UnityEngine.Vector2>> constrainedEdgePoints = new List<List<UnityEngine.Vector2>>();
            // 2.2.根据共享边信息，构建外轮廓边
            var mark_convex_visited = new bool[numConvex];
            for (int ci = 0; ci < numConvex; ++ci)
            {
                if (mark_convex_visited[ci]) continue;
                mark_convex_visited[ci] = true;

                var edgeIndics = new List<(int, int)>();
                var startConvexIndex = ci;
                var startEdgeIndex = 0;

                var currConexIndex = startConvexIndex;
                var currEdgeIndex = startConvexIndex;
                var loopTimes = 0;
                do
                {
                    if (++loopTimes > 100)
                    {
                        Debug.LogError("Triangulate, fetch external edge loop too times!");
                        break;
                    }
                    
                    var sharedEdgeIndex = convexSharedEdgeIndex[currConexIndex][currEdgeIndex];
                    if (sharedEdgeIndex != -1)
                    {
                        // 有共享边，跳转Convex
                        var sharedEdge = sharedEdges[sharedEdgeIndex];
                        var isConvexA = currConexIndex == sharedEdge.convexAIndex;
                        currConexIndex = isConvexA ? sharedEdge.convexBIndex : sharedEdge.convexAIndex;
                        currEdgeIndex = isConvexA ? sharedEdge.convexBEdgeIndex : sharedEdge.convexAEdgeIndex;

                        mark_convex_visited[currConexIndex] = true;
                    }
                    else
                    {
                        edgeIndics.Add((currConexIndex, currEdgeIndex));
                    }

                    var currConvex = ConvexShapes[currConexIndex];
                    currEdgeIndex = (currEdgeIndex + 1) % currConvex.NumEdges;
                } while(currConexIndex != startConvexIndex || currEdgeIndex != startEdgeIndex);

                // 取外轮廓
                if (edgeIndics.Count > 0)
                {
                    var polygon = new List<UnityEngine.Vector2>();
                    for (var edgeIndex = 0; edgeIndex < edgeIndics.Count; ++edgeIndex)
                    {
                        var convexIndex = edgeIndics[edgeIndex].Item1;
                        var convexEdgeIndex = edgeIndics[edgeIndex].Item2;
                        var convex = ConvexShapes[convexIndex];
                        var p = convex[convexEdgeIndex];
                        polygon.Add(new UnityEngine.Vector2(p[0], p[2]));
                    }
                    constrainedEdgePoints.Add(polygon);
                }
            }

            m_constraintEdges = constrainedEdgePoints;

            // 4.三角剖分 
            float TesselationMaximumTriangleArea = 0.0f;
            Triangulation = new Game.Utils.Triangulation.DelaunayTriangulation();
            Triangulation.Triangulate(pointsToTriangulate, TesselationMaximumTriangleArea, constrainedEdgePoints);
            return true;
        }

        // 调试使用
        private List<FSharedEdge> m_sharedEdges = null;
        private List<List<UnityEngine.Vector2>> m_constraintEdges = null;
        public void DrawGizmos()
        {
            var Colors = new UnityEngine.Color[] {
                UnityEngine.Color.red,
                UnityEngine.Color.green,
                UnityEngine.Color.blue,
            };
            // draw bounds
            UnityEngine.Vector3[] ClipPlanes = new UnityEngine.Vector3[]
            {
                new UnityEngine.Vector3(MinBounds[0], MinBounds[1], MinBounds[2]), new UnityEngine.Vector3(-1.0f, 0.0f, 0.0f), // Point + Normal
                new UnityEngine.Vector3(MaxBounds[0], MinBounds[1], MinBounds[2]), new UnityEngine.Vector3(0.0f, 0.0f, -1.0f),
                new UnityEngine.Vector3(MaxBounds[0], MinBounds[1], MaxBounds[2]), new UnityEngine.Vector3(1.0f, 0.0f, 0.0f),
                new UnityEngine.Vector3(MinBounds[0], MinBounds[1], MaxBounds[2]), new UnityEngine.Vector3(0.0f, 0.0f, 1.0f),
            };

            Gizmos.color = Color.magenta;
            for (int i = 0; i < 4; i++)
            {
                var p = ClipPlanes[i * 2];
                var n = ClipPlanes[i * 2 + 1];
                Gizmos.DrawCube(p, UnityEngine.Vector3.one * 0.1f);
                Gizmos.DrawLine(p, p + n);
            }

            // draw convex shape
            for (int i=0; i<ConvexShapes.Count; ++i)
            {
                var Color = Colors[i % Colors.Length];
                // ConvexShapes[i].DrawGizmos(Color);
            }

            // draw shared edges
            if (null != m_sharedEdges && m_sharedEdges.Count > 0)
            {
                for (int i=0; i<m_sharedEdges.Count; ++i)
                {
                    var sharedEdge = m_sharedEdges[i];
                    var convexIndex = sharedEdge.convexAIndex;
                    var convexEdgeIndex = sharedEdge.convexAEdgeIndex;
                    var convex = ConvexShapes[convexIndex];
                    var p0 = convex[convexEdgeIndex];
                    p0.y = 0;

                    var p1 = convex[(convexEdgeIndex+1)%convex.NumPoints];
                    p1.y = 0;

                    Gizmos.color = Color.cyan;
                    Gizmos.DrawLine(p0, p1);
                }
            }

            if (null != m_constraintEdges && m_constraintEdges.Count > 0)
            {
                Gizmos.color = Color.magenta;
                for (var i=0; i<m_constraintEdges.Count; ++i)
                {
                    var hole = m_constraintEdges[i];
                    for (int j=0, nj=hole.Count; j<nj; ++j)
                    {
                        var jj = (j+1) % nj;

                        var p0 = hole[j];
                        var p1 = hole[jj];
                        var v0 = new UnityEngine.Vector3(p0[0], 0, p0[1]);
                        var v1 = new UnityEngine.Vector3(p1[0], 0, p1[1]);

                        Gizmos.DrawLine(v0, v1);
                    }
                }
            }
        }
    }
}
