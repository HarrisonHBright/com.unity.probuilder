using UnityEngine;
using UnityEditor;
using System.Collections;
using System.Collections.Generic;
using ProBuilder2.Common;

namespace ProBuilder2.EditorCommon
{
	[CustomEditor(typeof(pb_BezierShape))]
	public class pb_BezierSplineTool : Editor
	{
		static GUIContent[] m_TangentModeIcons = new GUIContent[]
		{
			new GUIContent("Free"),
			new GUIContent("Aligned"),
			new GUIContent("Mirrored")
		};

		Color bezierPositionHandleColor = new Color(.01f, .8f, .99f, 1f);
		Color bezierTangentHandleColor = new Color(.6f, .6f, .6f, .8f);

		int m_currentIndex = -1;
		pb_BezierTangentMode m_TangentMode = pb_BezierTangentMode.Mirrored;

		pb_BezierShape m_Target = null;

		pb_Object m_CurrentObject
		{
			get
			{
				if(m_Target.mesh == null)
				{
					m_Target.mesh = m_Target.gameObject.AddComponent<pb_Object>();
					pb_EditorUtility.InitObject(m_Target.mesh);

				}

				return m_Target.mesh;
			}
		}

		List<pb_BezierPoint> m_Points { get { return m_Target.m_Points; } set { m_Target.m_Points = value; } }
		bool m_CloseLoop { get { return m_Target.m_CloseLoop; } set { m_Target.m_CloseLoop = value; } }
		float m_Radius { get { return m_Target.m_Radius; } set { m_Target.m_Radius = value; } }
		int m_Rows { get { return m_Target.m_Rows; } set { m_Target.m_Rows = value; } }
		int m_Columns { get { return m_Target.m_Columns; } set { m_Target.m_Columns = value; } }

		private GUIStyle _commandStyle = null;
		public GUIStyle commandStyle
		{
			get
			{
				if(_commandStyle == null)
				{
					_commandStyle = new GUIStyle(EditorGUIUtility.GetBuiltinSkin(EditorSkin.Inspector).FindStyle("Command"));
					_commandStyle.alignment = TextAnchor.MiddleCenter;
				}

				return _commandStyle;
			}
		}

		void OnEnable()
		{
			// SceneView.onSceneGUIDelegate += OnSceneGUI;
			m_Target = target as pb_BezierShape;
		}

		void OnDisable()
		{
			// SceneView.onSceneGUIDelegate -= OnSceneGUI;
		}

		public override void OnInspectorGUI()
		{
			EditorGUI.BeginChangeCheck();

			if(GUILayout.Button("Clear Points"))
			{
				m_Points.Clear();
			}

			if(GUILayout.Button("Add Point"))
			{
				if(m_Points.Count > 0)
				{
					m_Points.Add(new pb_BezierPoint(m_Points[m_Points.Count - 1].position,
						m_Points[m_Points.Count - 1].tangentIn,
						m_Points[m_Points.Count - 1].tangentOut));
				}
				else
				{
					m_Target.Init();
				}

				m_currentIndex = m_Points.Count - 1;

				SceneView.RepaintAll();
			}


			m_TangentMode = (pb_BezierTangentMode) GUILayout.Toolbar((int)m_TangentMode, m_TangentModeIcons, commandStyle);
			m_CloseLoop = EditorGUILayout.Toggle("Close Loop", m_CloseLoop);
			m_Radius = Mathf.Max(.001f, EditorGUILayout.FloatField("Radius", m_Radius));
			m_Rows = pb_Math.Clamp(EditorGUILayout.IntField("Rows", m_Rows), 3, 512);
			m_Columns = pb_Math.Clamp(EditorGUILayout.IntField("Columns", m_Columns), 3, 512);

			if(EditorGUI.EndChangeCheck())
				UpdateMesh();
		}

		void UpdateMesh()
		{
			m_Target.Refresh();
			pb_Editor.Refresh();
		}

		void OnSceneGUI()
		{
			int c = m_Points.Count;

			Matrix4x4 handleMatrix = Handles.matrix;
			Handles.matrix = m_Target.transform.localToWorldMatrix;

			EditorGUI.BeginChangeCheck();

			for(int index = 0; index < c; index++)
			{
				if(index < c -1 || m_CloseLoop)
				{
					Handles.DrawBezier(	m_Points[index].position,
										m_Points[(index+1)%c].position,
										m_Points[index].tangentOut,
										m_Points[(index+1)%c].tangentIn,
										Color.green,
										EditorGUIUtility.whiteTexture,
										1f);
				}

				// Handles.BeginGUI();
				// Handles.Label(m_Points[index].position, ("index: " + index));
				// Handles.EndGUI();

				pb_BezierPoint point = m_Points[index];

				if(m_currentIndex == index)
				{
					Vector3 prev = point.position;
					prev = Handles.PositionHandle(prev, Quaternion.identity);
					if(!pb_Math.Approx3(prev, point.position))
					{
						Vector3 dir = prev - point.position;
						point.position = prev;
						point.tangentIn += dir;
						point.tangentOut += dir;
					}

					Handles.color = bezierTangentHandleColor;

					if(m_CloseLoop || index > 0)
					{
						EditorGUI.BeginChangeCheck();

						point.tangentIn = Handles.PositionHandle(point.tangentIn, Quaternion.identity);
						if(EditorGUI.EndChangeCheck())
							point.EnforceTangentMode(pb_BezierTangentDirection.In, m_TangentMode);
						Handles.color = Color.blue;
						Handles.DrawLine(m_Points[index].position, m_Points[index].tangentIn);
					}
						
					if(m_CloseLoop || index < c - 1)
					{
						EditorGUI.BeginChangeCheck();
						point.tangentOut = Handles.PositionHandle(point.tangentOut, Quaternion.identity);
						if(EditorGUI.EndChangeCheck())
							point.EnforceTangentMode(pb_BezierTangentDirection.Out, m_TangentMode);
						Handles.color = Color.red;
						Handles.DrawLine(m_Points[index].position, m_Points[index].tangentOut);
					}

					m_Points[index] = point;
				}
				else
				{
					float size = HandleUtility.GetHandleSize(m_Points[index].position) * .05f;

					Handles.color = bezierPositionHandleColor;

					if (Handles.Button(m_Points[index].position, Quaternion.identity, size, size, Handles.DotCap))
						m_currentIndex = index;

					Handles.color = bezierTangentHandleColor;
					if(m_CloseLoop || index > 0)
						Handles.DrawLine(m_Points[index].position, m_Points[index].tangentIn);
					if(m_CloseLoop || index < c - 1)
						Handles.DrawLine(m_Points[index].position, m_Points[index].tangentOut);
					Handles.color = Color.white;
				}
			}

			Handles.matrix = handleMatrix;

			if( EditorGUI.EndChangeCheck() )
			{
				UpdateMesh();
			}
		}
	}
}