using UnityEngine;
using UnityEditor;
using System;
using System.Xml.Linq;

[CustomEditor(typeof(UnitTest_TileData), true)]
public class UnitTest_TileDataEditor : Editor
{
    // private SerializedProperty TriangleIndex_Slider = null;
    // private float TriangleIndex_Slider = 0;
    private int     DrawTriangleIndex = 0;
    private int     DrawConvexIndex = 0;

    private void OnEnable()
    {
        // TriangleIndex_Slider = serializedObject.FindProperty("");
    }

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        serializedObject.Update();

        serializedObject.ApplyModifiedProperties();

        var UnitTest = target as UnitTest_TileData;
        var TileData = UnitTest.TileData;

        if (TileData != null)
        {
            GUILayout.BeginVertical();
            {
                var ConvexShapeCount = TileData.ConvexShapes.Count;
                GUILayout.Label(string.Format("Totalt convex count: {0}, draw index£º{1}", ConvexShapeCount, DrawConvexIndex));
                
                if (TileData.Triangulation != null)
                {
                    var TriangleCount = TileData.Triangulation.TriangleSet.TriangleCount;
                    GUILayout.Label(string.Format("Totalt triangle count: {0}, draw index£º{1}", TriangleCount, DrawTriangleIndex));

                    GUILayout.BeginHorizontal();
                    {
                        if (GUILayout.Button("Prev triangle"))
                        {
                            DrawTriangleIndex = Mathf.Clamp(--DrawTriangleIndex, 0, TriangleCount - 1);
                            TileData.Triangulation.TriangleSet.DrawTriangle3D(DrawTriangleIndex, 0.0f, Color.green);
                        }
                        if (GUILayout.Button("Next triangle"))
                        {
                            DrawTriangleIndex = Mathf.Clamp(++DrawTriangleIndex, 0, TriangleCount - 1);
                            TileData.Triangulation.TriangleSet.DrawTriangle3D(DrawTriangleIndex, 0.0f, Color.green);
                        }
                    }
                    GUILayout.EndHorizontal();
                }

                GUILayout.BeginHorizontal();
                {
                    if (GUILayout.Button("Prev convex"))
                    {
                        --DrawConvexIndex;
                        DrawConvexIndex = (DrawConvexIndex + ConvexShapeCount) % ConvexShapeCount;
                        TileData.DrawConvex(DrawConvexIndex, Color.white);
                    }
                    if (GUILayout.Button("Next convex"))
                    {
                        ++DrawConvexIndex;
                        DrawConvexIndex = DrawConvexIndex % ConvexShapeCount;
                        TileData.DrawConvex(DrawConvexIndex, Color.white);
                    }
                }
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                { 
                    if (GUILayout.Button("Random test"))
                    { 
                        UnitTest.RandomTest();
                    }

                    if (GUILayout.Button("Do again"))
                    {
                        UnitTest.Triangulation();
                    }

                    if (GUILayout.Button("Dump obstacles"))
                    {
                        UnitTest.DumpObstacles();
                    }
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndVertical();
        }
    }
}
