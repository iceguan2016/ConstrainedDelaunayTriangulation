
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
            Vertices = InVertices;
        }

        public FConvexShape(List<UnityEngine.Vector3> InVertices)
        {
            Vertices = InVertices.ToArray();
        }

        public UnityEngine.Vector3[] Vertices = new UnityEngine.Vector3[0];

        public UnityEngine.Vector3 GetCenter()
        {
            UnityEngine.Vector3 Center = UnityEngine.Vector3.zero;
            int numPoints = Vertices.Length;

            if (numPoints < 3) return Center;

            for (int i = 0; i < numPoints; i++)
            {
                Center += Vertices[i];
            }
            Center *= (1.0f / numPoints);
            return Center;
        }

        public bool GetPlane(out UnityEngine.Vector3 OutPoint, out UnityEngine.Vector3 OutNormal)
        {
	        int numPoints = Vertices.Length;

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

        // Plane将本Convex裁剪为front和back，且保留本Convex数据不变
        public eSide Split(
            UnityEngine.Vector3 InClipPoint, 
            UnityEngine.Vector3 InClipNormal,
            out FConvexShape OutFrontConvex,
            out FConvexShape OutBackConvex)
        {
            OutFrontConvex = null;
            OutBackConvex = null;

            int NumPoints = Vertices.Length;
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
                if (GetPlane(out var OutPoint, out var OutNormal))
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
                            mid[j] = -W;
                        }
                        else if (InClipNormal[j] == -1.0f)
                        {
                            mid[j] = W;
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
                            mid[j] = -W;
                        }
                        else if (InClipNormal[j] == -1.0f)
                        {
                            mid[j] = W;
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

        public FConvexShape Clone()
        {
            return  new FConvexShape((UnityEngine.Vector3[])Vertices.Clone());
        }
    }

    public class FTileData
    { 
        public int TileX { get; private set; }
        public int TileZ { get; private set; }
        public UnityEngine.Vector3 MinBounds { get; private set; }
        public UnityEngine.Vector3 MaxBounds { get; private set; }

        public Game.Utils.Triangulation.DelaunayTriangulation Triangulation { get; private set; }

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

            Triangulation = new Game.Utils.Triangulation.DelaunayTriangulation();

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

            // 2.添加约束边
            return false; 
        }
    }
}
