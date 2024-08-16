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

        if (TileData != null && TileData.Triangulation != null)
        {
            GUILayout.BeginVertical();
            {
                var TriangleCount = TileData.Triangulation.TriangleSet.TriangleCount;
                GUILayout.Label(string.Format("Totalt triangle count: {0}, draw index£º{1}", TriangleCount, DrawTriangleIndex));

                //TriangleIndex_Slider = GUILayout.HorizontalSlider(TriangleIndex_Slider, 0, TriangleCount - 1, GUILayout.Width(200));
                //TriangleIndex_Slider = Mathf.RoundToInt(TriangleIndex_Slider);

                GUILayout.BeginHorizontal();
                { 
                    if (GUILayout.Button("Prev triangle"))
                    {
                        DrawTriangleIndex = Mathf.Clamp(--DrawTriangleIndex, 0, TriangleCount-1);
                        TileData.Triangulation.TriangleSet.DrawTriangle3D(DrawTriangleIndex, 0.0f, Color.green);
                    }
                    if (GUILayout.Button("Next triangle"))
                    {
                        DrawTriangleIndex = Mathf.Clamp(++DrawTriangleIndex, 0, TriangleCount-1);
                        TileData.Triangulation.TriangleSet.DrawTriangle3D(DrawTriangleIndex, 0.0f, Color.green);
                    }
                }
                GUILayout.EndHorizontal();

                //if (GUILayout.Button("Draw Triangle"))
                //{
                //    TileData.Triangulation.TriangleSet.DrawTriangle3D(DrawTriangleIndex, 0.0f, Color.green);
                //}
            }
            GUILayout.EndVertical();
        }
    }
}
