using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class BoxShape : MonoBehaviour
{
    public Vector3 HalfExtent = Vector3.one;

    public Navmesh.FConvexShape GetConvexShape()
    {
        var Position = transform.position;
        var Rotation = transform.rotation;

        Vector3[] Vertices = new Vector3[] {
                Position + Rotation * new Vector3( HalfExtent.x, -HalfExtent.y,  HalfExtent.z), // 0
                Position + Rotation * new Vector3(-HalfExtent.x, -HalfExtent.y,  HalfExtent.z), // 1
                Position + Rotation * new Vector3(-HalfExtent.x, -HalfExtent.y, -HalfExtent.z), // 2
                Position + Rotation * new Vector3( HalfExtent.x, -HalfExtent.y, -HalfExtent.z), // 3
        };

        return new Navmesh.FConvexShape(Vertices);
    }

    public static void DrawWireCube(Vector3 InPosition, Quaternion InRotation, Vector3 InHalfExtent)
    {
        Vector3[] Vertices = new Vector3[] {
                InPosition + InRotation * new Vector3( InHalfExtent.x, -InHalfExtent.y,  InHalfExtent.z), // 0
                InPosition + InRotation * new Vector3(-InHalfExtent.x, -InHalfExtent.y,  InHalfExtent.z), // 1
                InPosition + InRotation * new Vector3(-InHalfExtent.x, -InHalfExtent.y, -InHalfExtent.z), // 2
                InPosition + InRotation * new Vector3( InHalfExtent.x, -InHalfExtent.y, -InHalfExtent.z), // 3

                InPosition + InRotation * new Vector3( InHalfExtent.x,  InHalfExtent.y,  InHalfExtent.z), // 4
                InPosition + InRotation * new Vector3(-InHalfExtent.x,  InHalfExtent.y,  InHalfExtent.z), // 5
                InPosition + InRotation * new Vector3(-InHalfExtent.x,  InHalfExtent.y, -InHalfExtent.z), // 6
                InPosition + InRotation * new Vector3( InHalfExtent.x,  InHalfExtent.y, -InHalfExtent.z), // 7
            };

        int[] Indices = new int[] {
                0, 1, 1, 2, 2, 3, 3, 0,
                4, 5, 5, 6, 6, 7, 7, 4,
                0, 4, 1, 5, 2, 6, 3, 7
            };

        for (int i = 0; i < Indices.Length; i += 2)
        {
            Gizmos.DrawLine(Vertices[Indices[i]], Vertices[Indices[i + 1]]);
        }
    }

    private void OnDrawGizmos()
    {
        var Position = transform.position;
        var Rotation = transform.rotation;

        var OldColor = Gizmos.color;

        Gizmos.color = Color.gray;
        DrawWireCube(Position, Rotation, HalfExtent);

        Gizmos.color = OldColor;
    }
}
