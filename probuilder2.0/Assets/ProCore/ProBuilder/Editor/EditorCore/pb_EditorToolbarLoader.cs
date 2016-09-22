using UnityEngine;
using System.Collections.Generic;
using ProBuilder2.Common;
using Actions = ProBuilder2.Actions;
using System.Linq;

namespace ProBuilder2.EditorCommon
{
	public class pb_EditorToolbarLoader
	{
		public delegate pb_MenuAction OnLoadActionsDelegate();
		public static OnLoadActionsDelegate onLoadMenu;

		private static List<pb_MenuAction> _defaults;

		public static void RegisterMenuItem(OnLoadActionsDelegate getMenuAction)
		{
			if(onLoadMenu != null)
			{
				onLoadMenu -= getMenuAction;
				onLoadMenu += getMenuAction;
			}
			else
			{
				onLoadMenu = getMenuAction;
			}
		}

		public static void UnRegisterMenuItem(OnLoadActionsDelegate getMenuAction)
		{
			onLoadMenu -= getMenuAction;
		}

		public static T GetInstance<T>() where T : pb_MenuAction, new()
		{
			T instance = (T) GetActions().FirstOrDefault(x => x is T);

			if(instance == null)
			{
				instance = new T();
				if(_defaults != null)
					_defaults.Add(instance);
				else
					_defaults = new List<pb_MenuAction>() { instance };
			}

			return instance;
		}

		public static List<pb_MenuAction> GetActions(bool forceReload = false)
		{
			if(_defaults != null && !forceReload)
				return _defaults;

			_defaults = new List<pb_MenuAction>()
			{
				// tools
				new Actions.OpenShapeEditor(),
				new Actions.OpenMaterialEditor(),
				new Actions.OpenUVEditor(),
				new Actions.OpenVertexColorEditor(),
				new Actions.OpenSmoothingEditor(),

				new Actions.ToggleSelectHidden(),
				new Actions.ToggleSelectBackFaces(),
				new Actions.ToggleHandleAlignment(),
				new Actions.ToggleDragSelectionMode(),
				new Actions.ToggleDragRectMode(),

				// selection
				new Actions.GrowSelection(),
				new Actions.ShrinkSelection(),
				new Actions.InvertSelection(),
				new Actions.SelectEdgeLoop(),
				new Actions.SelectEdgeRing(),
				new Actions.SelectHole(),
				new Actions.SelectVertexColor(),
				new Actions.SelectMaterial(),

				// object
				new Actions.MergeObjects(),
				new Actions.MirrorObjects(),
				new Actions.FlipObjectNormals(),
				new Actions.SubdivideObject(),
				new Actions.FreezeTransform(),
				new Actions.CenterPivot(),
				new Actions.ConformObjectNormals(),
				new Actions.TriangulateObject(),
				new Actions.GenerateUV2(),
				new Actions.ProBuilderize(),

				// All
				new Actions.SetPivotToSelection(),

				// Faces (All)
				new Actions.DeleteFaces(),
				new Actions.DetachFaces(),
				new Actions.ExtrudeFaces(),

				// Face
				new Actions.ConformFaceNormals(),
				new Actions.FlipFaceEdge(),
				new Actions.FlipFaceNormals(),
				new Actions.MergeFaces(),
				new Actions.SubdivideFaces(),
				new Actions.TriangulateFaces(),

				// Edge
				new Actions.BridgeEdges(),
				new Actions.BevelEdges(),
				new Actions.ConnectEdges(),
				new Actions.ExtrudeEdges(),
				new Actions.InsertEdgeLoop(),
				new Actions.SubdivideEdges(),

				// Vertex
				new Actions.CollapseVertices(),
				new Actions.WeldVertices(),
				new Actions.ConnectVertices(),
				new Actions.FillHole(),
				// new Actions.CreatePolygon(),
				new Actions.SplitVertices(),

				// Entity
				new Actions.SetEntityType_Detail(),
				new Actions.SetEntityType_Mover(),
				new Actions.SetEntityType_Collider(),
				new Actions.SetEntityType_Trigger(),
			};

			if(onLoadMenu != null)
			{
				foreach(OnLoadActionsDelegate del in onLoadMenu.GetInvocationList())
					_defaults.Add(del());
			}

			_defaults.Sort(pb_MenuAction.CompareActionsByGroupAndPriority);

			return _defaults;
		}
	}
}
