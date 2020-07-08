﻿using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.ProBuilder.Actions;
using UnityEngine;
using UnityEngine.ProBuilder;
using UnityEngine.ProBuilder.MeshOperations;
using UnityEngine.TestTools.Utils;
using Math = System.Math;
using RaycastHit = UnityEngine.ProBuilder.RaycastHit;
using UHandleUtility = UnityEditor.HandleUtility;

namespace UnityEditor.ProBuilder
{
    [CustomEditor(typeof(PolygonalCut))]
    public class PolygonalCutEditor : Editor
    {
#region Variables

        static Color k_HandleColor = new Color(.8f, .8f, .8f, 1f);
        static Color k_HandleColorAddNewVertex = new Color(.01f, .9f, .3f, 1f);
        static Color k_HandleColorAddVertexOnEdge = new Color(.3f, .01f, .9f, 1f);
        static Color k_HandleColorUseExistingVertex = new Color(.01f, .5f, 1f, 1f);
        static Color k_HandleColorModifyVertex = new Color(1f, .75f, .0f, 1f);


        const float k_HandleSize = .05f;

        static Color k_LineMaterialBaseColor = new Color(0f, 136f / 255f, 1f, 1f);
        static Color k_LineMaterialHighlightColor = new Color(0f, 200f / 255f, 170f / 200f, 1f);

        static Color k_ClosingLineMaterialBaseColor = new Color(136f / 255f, 1f, 0f, 1f);
        static Color k_ClosingLineMaterialHighlightColor = new Color(1f, 170f / 200f, 0f, 1f);

        static Color k_InvalidLineMaterialColor = Color.red;

        Material m_LineMaterial;
        Mesh m_LineMesh = null;

        Material m_ClosingLineMaterial;
        Mesh m_ClosingLineMesh = null;

        Color m_CurrentHandleColor = k_HandleColor;

        static Material CreateHighlightLineMaterial()
        {
            Material mat = new Material(Shader.Find("Hidden/ProBuilder/ScrollHighlight"));
            mat.SetColor("_Highlight", k_LineMaterialHighlightColor);
            mat.SetColor("_Base", k_LineMaterialBaseColor);
            return mat;
        }

        static Material CreateClosingLineMaterial()
        {
            Material mat = new Material(Shader.Find("Hidden/ProBuilder/ScrollHighlight"));
            mat.SetColor("_Highlight", k_ClosingLineMaterialHighlightColor);
            mat.SetColor("_Base", k_ClosingLineMaterialBaseColor);
            return mat;
        }

        PolygonalCut polygonalCut
        {
            get { return target as PolygonalCut; }
        }

        private Face m_TargetFace = null;
        private Face m_CurrentFace;
        private Vector3 m_CurrentPositionToAdd = Vector3.positiveInfinity;
        private Vector3 m_CurrentPositionNormal = Vector3.up;
        public PolygonalCut.VertexType m_CurrentVertexType = PolygonalCut.VertexType.None;

        private int m_ControlId;
        bool m_PlacingPoint = false;
        bool m_SnapPoint = false;
        bool m_ModifyPoint = false;
        int m_SelectedIndex = -2;

        #endregion

#region Unity Callbacks

        void OnEnable()
        {
            if (polygonalCut == null)
            {
                DestroyImmediate(this);
                return;
            }

            ProBuilderEditor.selectModeChanged += OnSelectModeChanged;

            m_LineMesh = new Mesh();
            m_LineMaterial = CreateHighlightLineMaterial();

            m_ClosingLineMesh = new Mesh();
            m_ClosingLineMaterial = CreateClosingLineMaterial();

            Undo.undoRedoPerformed += UndoRedoPerformed;
#if UNITY_2019_1_OR_NEWER
            SceneView.duringSceneGui += DuringSceneGUI;
#else
            SceneView.onSceneGUIDelegate += DuringSceneGUI;
#endif
            EditorApplication.update += Update;
            Undo.undoRedoPerformed += UndoRedoPerformed;
        }

        void OnDisable()
        {
            // Quit Edit mode when the object gets de-selected.
            if (polygonalCut != null)
                polygonalCut.polygonEditMode = PolygonalCut.PolygonEditMode.None;

            ProBuilderEditor.selectModeChanged -= OnSelectModeChanged;
#if UNITY_2019_1_OR_NEWER
            SceneView.duringSceneGui -= DuringSceneGUI;
#else
            SceneView.onSceneGUIDelegate -= DuringSceneGUI;
#endif
            Undo.undoRedoPerformed -= UndoRedoPerformed;
            EditorApplication.update -= Update;

            DestroyImmediate(m_LineMesh);
            DestroyImmediate(m_LineMaterial);

            DestroyImmediate(m_ClosingLineMesh);
            DestroyImmediate(m_ClosingLineMaterial);

            //Removing the script from the object
            DestroyImmediate(polygonalCut);
        }

        void Update()
        {
            if (polygonalCut.mesh != null && polygonalCut.polygonEditMode == PolygonalCut.PolygonEditMode.Add && m_LineMaterial != null)
                m_LineMaterial.SetFloat("_EditorTime", (float)EditorApplication.timeSinceStartup);
            if (polygonalCut.mesh != null && polygonalCut.polygonEditMode == PolygonalCut.PolygonEditMode.Add && m_ClosingLineMaterial != null)
                m_ClosingLineMaterial.SetFloat("_EditorTime", (float)EditorApplication.timeSinceStartup);

            if (polygonalCut.CutEnded)
                DoPolygonalCut();
        }

#endregion

#region Callbacks

        private void DuringSceneGUI(SceneView obj)
        {
            if (polygonalCut.polygonEditMode == PolygonalCut.PolygonEditMode.None)
                return;

            if (m_LineMaterial != null)
            {
                m_LineMaterial.SetPass(0);
                Graphics.DrawMeshNow(m_LineMesh, polygonalCut.transform.localToWorldMatrix, 0);
            }
            if (m_ClosingLineMaterial != null)
            {
                m_ClosingLineMaterial.SetPass(0);
                Graphics.DrawMeshNow(m_ClosingLineMesh, polygonalCut.transform.localToWorldMatrix, 0);
            }

            Event currentEvent = Event.current;

            DoExistingPointsGUI();

            if (currentEvent.type == EventType.KeyDown)
                HandleKeyEvent(currentEvent);

            if (EditorHandleUtility.SceneViewInUse(currentEvent))
                return;

            m_ControlId = GUIUtility.GetControlID(FocusType.Passive);
            if (currentEvent.type == EventType.Layout)
                HandleUtility.AddDefaultControl(m_ControlId);

            DoPointPrePlacement();
            DoPointPlacement();
        }

        private void UndoRedoPerformed()
        {
            if (m_LineMesh != null)
                DestroyImmediate(m_LineMesh);

            if (m_LineMaterial != null)
                DestroyImmediate(m_LineMaterial);

            m_LineMesh = new Mesh();
            m_LineMaterial = CreateHighlightLineMaterial();

            if (m_ClosingLineMesh != null)
                DestroyImmediate(m_ClosingLineMesh);

            if (m_ClosingLineMaterial != null)
                DestroyImmediate(m_ClosingLineMaterial);

            m_ClosingLineMesh = new Mesh();
            m_ClosingLineMaterial = CreateClosingLineMaterial();

            if(polygonalCut.m_verticesToAdd.Count == 0)
                m_TargetFace = null;

            RebuildPolygonalShape(polygonalCut);
        }

        private void OnSelectModeChanged(SelectMode obj)
        {
        }

#endregion

#region Point Placement

        void DoPointPrePlacement()
        {
            Event evt = Event.current;
            EventType evtType = evt.type;

            if (evtType== EventType.Repaint
                && polygonalCut.polygonEditMode == PolygonalCut.PolygonEditMode.Add)
            {
                m_SnapPoint = (evt.modifiers & EventModifiers.Control) != 0;
                m_ModifyPoint = evt.shift;

                if(!m_SnapPoint && !m_ModifyPoint && !m_PlacingPoint)
                    m_SelectedIndex = -1;

                if (UpdateHitPosition())
                {
                    if( (m_CurrentVertexType & (PolygonalCut.VertexType.ExistingVertex | PolygonalCut.VertexType.VertexInShape)) != 0)
                        m_CurrentHandleColor = m_ModifyPoint ? k_HandleColorModifyVertex : k_HandleColorUseExistingVertex;
                    else if ((m_CurrentVertexType & PolygonalCut.VertexType.AddedOnEdge) != 0)
                        m_CurrentHandleColor = k_HandleColorAddVertexOnEdge;
                    else
                        m_CurrentHandleColor = k_HandleColorAddNewVertex;
                }
                else
                {
                    m_CurrentPositionToAdd = Vector3.positiveInfinity;
                    m_CurrentVertexType = PolygonalCut.VertexType.None;
                    m_CurrentHandleColor = k_HandleColor;
                }
            }
        }

        private bool UpdateHitPosition()
        {
            Event evt = Event.current;

            Ray ray = UHandleUtility.GUIPointToWorldRay(evt.mousePosition);
            RaycastHit pbHit;

            m_CurrentFace = null;

            if (UnityEngine.ProBuilder.HandleUtility.FaceRaycast(ray, polygonalCut.mesh, out pbHit))
            {
                m_CurrentPositionToAdd = pbHit.point;
                m_CurrentPositionNormal = pbHit.normal;
                m_CurrentFace = polygonalCut.mesh.faces[pbHit.face];
                m_CurrentVertexType = PolygonalCut.VertexType.None;

                if(m_SnapPoint || m_ModifyPoint)
                    CheckPointProjectionInPolyshape();

                if (m_CurrentVertexType == PolygonalCut.VertexType.None && !m_ModifyPoint)
                    CheckPointProjectionInMesh();

                return true;
            }

            return false;
        }

        private void CheckPointProjectionInPolyshape()
        {
            float snapDistance = 0.1f;
            for (int i = 0; i < polygonalCut.m_verticesToAdd.Count; i++)
            {
                PolygonalCut.InsertedVertexData vertexData = polygonalCut.m_verticesToAdd[i];
                if (UnityEngine.ProBuilder.Math.Approx3(vertexData.m_Position,
                    m_CurrentPositionToAdd,
                    snapDistance))
                {
                    snapDistance = Vector3.Distance(vertexData.m_Position, m_CurrentPositionToAdd);
                    if(!m_ModifyPoint)
                        m_CurrentPositionToAdd = vertexData.m_Position;
                    m_CurrentVertexType = vertexData.m_Type | PolygonalCut.VertexType.VertexInShape;
                    m_SelectedIndex = i;
                }
            }
        }

        private void CheckPointProjectionInMesh()
        {
            m_CurrentVertexType = PolygonalCut.VertexType.NewVertex;
            bool snapedOnVertex = false;
            float snapDistance = 0.1f;
            int bestIndex = -1;
            float bestDistance = Mathf.Infinity;

            List<Vertex> vertices = polygonalCut.mesh.GetVertices().ToList();
            List<Edge> peripheralEdges = WingedEdge.SortEdgesByAdjacency(m_CurrentFace);
            if (m_TargetFace != null && m_CurrentFace != m_TargetFace)
                peripheralEdges = WingedEdge.SortEdgesByAdjacency(m_TargetFace);

            for (int i = 0; i < peripheralEdges.Count; i++)
            {
                if (m_TargetFace == null || (m_TargetFace == m_CurrentFace && m_SnapPoint))
                {
                    if (UnityEngine.ProBuilder.Math.Approx3(vertices[peripheralEdges[i].a].position,
                        m_CurrentPositionToAdd,
                        snapDistance))
                    {
                        bestIndex = i;
                        snapedOnVertex = true;
                        break;
                    }
                    else
                    {
                        float dist = UnityEngine.ProBuilder.Math.DistancePointLineSegment(
                            m_CurrentPositionToAdd,
                            vertices[peripheralEdges[i].a].position,
                            vertices[peripheralEdges[i].b].position);

                        if (dist < Mathf.Min(snapDistance, bestDistance))
                        {
                            bestIndex = i;
                            bestDistance = dist;
                        }
                    }
                }
                else if(m_CurrentFace != m_TargetFace)
                {
                    float edgeDist = UnityEngine.ProBuilder.Math.DistancePointLineSegment(m_CurrentPositionToAdd,
                        vertices[peripheralEdges[i].a].position,
                        vertices[peripheralEdges[i].b].position);

                    float vertexDist = Vector3.Distance(m_CurrentPositionToAdd,
                        vertices[peripheralEdges[i].a].position);

                    if (edgeDist < vertexDist && edgeDist < bestDistance)
                    {
                        bestIndex = i;
                        bestDistance = edgeDist;
                        snapedOnVertex = false;
                    }
                    //always prioritize vertex on edge
                    else if (vertexDist <= bestDistance)
                    {
                        bestIndex = i;
                        bestDistance = vertexDist;
                        snapedOnVertex = true;
                    }
                }
            }

            //We found a close vertex
            if (snapedOnVertex)
            {
                m_CurrentPositionToAdd = vertices[peripheralEdges[bestIndex].a].position;
                m_CurrentVertexType = PolygonalCut.VertexType.ExistingVertex;
                m_SelectedIndex = -1;
            }
            //If not, did we found a close edge?
            else if (bestIndex >= 0)
            {
                if (m_TargetFace == null || m_TargetFace == m_CurrentFace)
                {
                    Vector3 left = vertices[peripheralEdges[bestIndex].a].position,
                        right = vertices[peripheralEdges[bestIndex].b].position;

                    float x = (m_CurrentPositionToAdd - left).magnitude;
                    float y = (m_CurrentPositionToAdd - right).magnitude;

                    m_CurrentPositionToAdd = left + (x / (x + y)) * (right - left);
                }
                else //if(m_CurrentFace != m_TargetFace)
                {
                    Vector3 a = m_CurrentPositionToAdd -
                                vertices[peripheralEdges[bestIndex].a].position;
                    Vector3 b = vertices[peripheralEdges[bestIndex].b].position -
                                vertices[peripheralEdges[bestIndex].a].position;

                    float angle = Vector3.Angle(b, a);
                    m_CurrentPositionToAdd = Vector3.Magnitude(a) * Mathf.Cos(angle * Mathf.Deg2Rad) * b / Vector3.Magnitude(b);
                    m_CurrentPositionToAdd += vertices[peripheralEdges[bestIndex].a].position;
                }

                m_CurrentVertexType = PolygonalCut.VertexType.AddedOnEdge;
                m_SelectedIndex = -1;
            }
        }

        private void DoPointPlacement()
        {
            Event evt = Event.current;
            EventType evtType = evt.type;

            if (m_PlacingPoint || m_ModifyPoint)
            {
                if (evtType == EventType.MouseDrag)
                {
                    if (UpdateHitPosition())
                    {
                        evt.Use();
                        polygonalCut.m_verticesToAdd[m_SelectedIndex].m_Position = m_CurrentPositionToAdd;
                        RebuildPolygonalShape(false);
                        SceneView.RepaintAll();
                    }

                }

                if (evtType == EventType.MouseUp ||
                    evtType == EventType.Ignore ||
                    evtType == EventType.KeyDown ||
                    evtType == EventType.KeyUp)
                {
                    evt.Use();
                    m_PlacingPoint = false;
                    m_SelectedIndex = -1;
                    SceneView.RepaintAll();
                }
            }
            else
            if (polygonalCut.polygonEditMode == PolygonalCut.PolygonEditMode.Add
                && evtType == EventType.MouseDown
                && HandleUtility.nearestControl == m_ControlId)
            {
                int polyCount = polygonalCut.m_verticesToAdd.Count;
                if (m_CurrentPositionToAdd != Vector3.positiveInfinity
                    && (polyCount == 0 || m_SelectedIndex != polyCount - 1))
                {
                    UndoUtility.RecordObject(polygonalCut, "Add Vertex On Face");

                    if (m_TargetFace == null)
                        m_TargetFace = m_CurrentFace;

                    polygonalCut.m_verticesToAdd.Add(new PolygonalCut.InsertedVertexData(m_CurrentPositionToAdd,m_CurrentPositionNormal, m_CurrentVertexType));

                    m_PlacingPoint = true;
                    m_SelectedIndex = polygonalCut.m_verticesToAdd.Count - 1;

                    RebuildPolygonalShape();

                    if (CheckForEditionEnd())
                    {
                        DoPolygonalCut();
                    }

                    evt.Use();
                }
            }
        }

        // Returns a local space point,
        Vector3 GetPointInLocalSpace(Vector3 point)
        {
            var trs = polygonalCut.transform;
            return trs.InverseTransformPoint(point);
        }

#endregion

#region Vertex Insertion

    private List<Vertex> InsertVertices()
    {
        List<Vertex> newVertices = new List<Vertex>();

        foreach (PolygonalCut.InsertedVertexData vertexData in polygonalCut.m_verticesToAdd)
        {
            Vertex vertex = null;
            switch (vertexData.m_Type)
            {
                case PolygonalCut.VertexType.ExistingVertex:
                case PolygonalCut.VertexType.VertexInShape:
                    newVertices.Add(InsertVertexOnExistingVertex(vertexData.m_Position));
                    break;
                case PolygonalCut.VertexType.AddedOnEdge:
                    newVertices.Add(InsertVertexOnEdge(vertexData.m_Position));
                    break;
                case PolygonalCut.VertexType.NewVertex:
                    newVertices.Add(polygonalCut.mesh.InsertVertexInMeshSimple(vertexData.m_Position,vertexData.m_Normal));
                    break;
                default:
                    break;
            }
        }

        return newVertices;
    }

    private Vertex InsertVertexOnExistingVertex(Vector3 position)
    {
        Vertex vertex = null;
        List<Vertex> vertices = polygonalCut.mesh.GetVertices().ToList();
        for (int vertIndex = 0; vertIndex < vertices.Count; vertIndex++)
        {
            if (UnityEngine.ProBuilder.Math.Approx3(vertices[vertIndex].position, position))
            {
                vertex = vertices[vertIndex];
                break;
            }
        }

        return vertex;
    }

    private Vertex InsertVertexOnEdge(Vector3 vertexPosition)
    {
        List<Vertex> vertices = polygonalCut.mesh.GetVertices().ToList();
        List<Edge> peripheralEdges = WingedEdge.SortEdgesByAdjacency(m_TargetFace);

        int bestIndex = -1;
        float bestDistance = Mathf.Infinity;
        for (int i = 0; i < peripheralEdges.Count; i++)
        {
            float dist = UnityEngine.ProBuilder.Math.DistancePointLineSegment(vertexPosition,
                    vertices[peripheralEdges[i].a].position,
                    vertices[peripheralEdges[i].b].position);

            if (dist < bestDistance)
            {
                bestIndex = i;
                bestDistance = dist;
            }
        }

        Vertex v = polygonalCut.mesh.InsertVertexOnEdge(peripheralEdges[bestIndex], vertexPosition);
        return v;
    }

#endregion



#region Do Cut

    private bool CheckForEditionEnd()
    {
        if (m_TargetFace == null || polygonalCut.m_verticesToAdd.Count < 2)
            return false;

        if (polygonalCut.CutEnded)
            return true;

        if (VertexInsertion.EndOnClicToStartPoint)
        {
            return UnityEngine.ProBuilder.Math.Approx3(polygonalCut.m_verticesToAdd[0].m_Position,polygonalCut.m_verticesToAdd[polygonalCut.m_verticesToAdd.Count - 1].m_Position);
        }

        if (VertexInsertion.EndOnEdgeConnection)
        {
            return (polygonalCut.m_verticesToAdd[0].m_Type
                        & (PolygonalCut.VertexType.AddedOnEdge | PolygonalCut.VertexType.ExistingVertex)) != 0
                   && (polygonalCut.m_verticesToAdd[polygonalCut.m_verticesToAdd.Count - 1].m_Type
                       & (PolygonalCut.VertexType.AddedOnEdge | PolygonalCut.VertexType.ExistingVertex)) != 0;
        }

        return false;
    }

    private ActionResult DoPolygonalCut()
    {
        if (m_TargetFace == null || polygonalCut.m_verticesToAdd.Count < 2)
        {
            //Debug.LogError("Not enough points to define a cut");
            return ActionResult.NoSelection;
        }

        if (!polygonalCut.IsALoop)
        {
            if (VertexInsertion.ConnectToStartPoint)
            {
                polygonalCut.m_verticesToAdd.Add(new PolygonalCut.InsertedVertexData(
                    polygonalCut.m_verticesToAdd[0].m_Position,
                    PolygonalCut.VertexType.VertexInShape));
            }
            else
            {
//                PruneOrphans();
//                while ( (polygonalCut.m_verticesToAdd[0].m_Type & (PolygonalCut.VertexType.ExistingVertex | PolygonalCut.VertexType.AddedOnEdge)) == 0)
//                {
//                    remove and destroy polygonalCut.m_verticesToAdd[0]
//                }
            }
        }

        UndoUtility.RecordObject(polygonalCut.mesh, "Add Face To Mesh");

        List<Vertex> cutVertices = InsertVertices();

        if (polygonalCut.IsALoop)
        {
            List<Vertex> meshVertices = polygonalCut.mesh.GetVertices().ToList();
            int[] loopCut = cutVertices.Select(vert => meshVertices.IndexOf(vert)).ToArray();
            Face f = polygonalCut.mesh.CreatePolygon(loopCut, false);

            if(f == null)
                return new ActionResult(ActionResult.Status.Failure, "Cut Shape is not valid");

            Vector3 nrm = UnityEngine.ProBuilder.Math.Normal(polygonalCut.mesh, f);
            Vector3 targetNrm = UnityEngine.ProBuilder.Math.Normal(polygonalCut.mesh, m_TargetFace);
            if(Vector3.Dot(nrm,targetNrm) < 0f)
                f.Reverse();
        }

        int connectionsToFaceBorders = polygonalCut.ConnectionsToFaceBordersCount;

        switch (connectionsToFaceBorders)
        {
            case 0:
                CreateFaceWithHole(m_TargetFace, cutVertices);
                break;
            case 1:
                List<int[]> indexes = ComputePolygons(m_TargetFace, cutVertices);
                foreach (int[] polygon in indexes)
                {
                    Face face = polygonalCut.mesh.CreatePolygon(polygon,false);

                    int[] complementaryPolygon = GetComplementaryPolygons(polygon);
                    if (complementaryPolygon != null)
                    {
                        Face compFace = polygonalCut.mesh.CreatePolygon(complementaryPolygon, false);
                        MergeElements.Merge(polygonalCut.mesh, new[] {face, compFace});
                    }
                }
                break;
            default:
                List<int[]> verticesIndexes = ComputePolygons(m_TargetFace, cutVertices);

                foreach (int[] polygon in verticesIndexes)
                {
                    polygonalCut.mesh.CreatePolygon(polygon,false);
                }
            break;
        }

        polygonalCut.mesh.DeleteFace(m_TargetFace);

        DestroyImmediate(polygonalCut);

        return ActionResult.Success;
    }

    private Face CreateFaceWithHole(Face face, List<Vertex> cutVertices)
    {
        List<Vertex> meshVertices = polygonalCut.mesh.GetVertices().ToList();

        List<Edge> peripheralEdges = WingedEdge.SortEdgesByAdjacency(face);
        List<int> indexes = peripheralEdges.Select(edge => edge.a).ToList();

        IList<IList<int>> holes = new List<IList<int>>();
        List<int> hole = cutVertices.Select(vert => meshVertices.IndexOf(vert)).ToList();
        holes.Add(hole);

        Face newFace = polygonalCut.mesh.CreatePolygonWithHole(indexes, holes);

        return newFace;
    }

    private List<int[]> ComputePolygons(Face face, List<Vertex> cutVertices)
    {
        List<int[]> polygons =new List<int[]>();

        //Get Vertices from the mesh
        List<Vertex> meshVertices = polygonalCut.mesh.GetVertices().ToList();
        Dictionary<int, int> sharedToUnique = polygonalCut.mesh.sharedVertexLookup;

        List<int> cutVerticesSharedIndex = cutVertices.Select(vert => sharedToUnique[meshVertices.IndexOf(vert)]).ToList();

        //Parse peripheral edges to unique id and find a common point between the mesh and the cut
        List<Edge> peripheralEdges = WingedEdge.SortEdgesByAdjacency(face);
        List<Edge> peripheralEdgesUnique = new List<Edge>();
        int startIndex = -1;
        for (int i = 0; i < peripheralEdges.Count; i++)
        {
            Edge e = peripheralEdges[i];
            Edge eShared = new Edge();
            eShared.a = sharedToUnique[e.a];
            eShared.b = sharedToUnique[e.b];

            peripheralEdgesUnique.Add(eShared);

            if (cutVerticesSharedIndex.Contains(eShared.a) && startIndex == -1)
                startIndex = i;
        }

        //Create a polygon for each cut reaching the mesh edges
        List<int> polygon = new List<int>();
        Edge previousEdge = new Edge(-1,-1);
        for (int i = startIndex; i <= peripheralEdgesUnique.Count + startIndex; i++)
        {
             Edge e = peripheralEdgesUnique[i % peripheralEdgesUnique.Count];

             polygon.Add(peripheralEdges[i % peripheralEdgesUnique.Count].a);
             if(polygon.Count > 1 && cutVerticesSharedIndex.Contains(e.a)) // get next vertex
             {
                 List<int> closure = ClosePolygonCut(polygon[0], previousEdge, e.a, cutVerticesSharedIndex);
                 polygon.AddRange(closure);
                 polygons.Add(polygon.ToArray());

                 //Start a new polygon
                 polygon = new List<int>();
                 polygon.Add(peripheralEdges[i % peripheralEdgesUnique.Count].a);
             }

             previousEdge = e;
        }

        return polygons;
    }

    private List<int> ClosePolygonCut(int polygonFirstVertex ,Edge previousEdge, int currentIndex, List<int> cutIndexes)
    {
        List<int> closure = new List<int>();
        List<Vertex> meshVertices = polygonalCut.mesh.GetVertices().ToList();
        IList<SharedVertex> uniqueIdToVertexIndex = polygonalCut.mesh.sharedVertices;

        Vector3 previousEdgeDir = meshVertices[uniqueIdToVertexIndex[previousEdge.b][0]].position -
                                  meshVertices[uniqueIdToVertexIndex[previousEdge.a][0]].position;
        previousEdgeDir.Normalize();

        int bestSuccessorIndex = -1;
        int successorDirection = 0;
        float bestCandidate = Mathf.Infinity;
        for (int i = 0; i < cutIndexes.Count; i++)
        {
            //Find the current point in the polygon
            if (cutIndexes[i] == currentIndex)
            {
                if (i > 0 || polygonalCut.IsALoop)
                {
                    int previousIndex = (i > 0) ? i - 1 : (cutIndexes.Count - 1);
                    int previousVertexIndex = cutIndexes[previousIndex];
                    Vector3 previousVertexDir = meshVertices[uniqueIdToVertexIndex[previousVertexIndex][0]].position -
                                                meshVertices[uniqueIdToVertexIndex[currentIndex][0]].position;
                    previousVertexDir.Normalize();

                    float similarityToPrevious = Vector3.Dot(previousEdgeDir, previousVertexDir);
                    if (similarityToPrevious < bestCandidate) //Go to previous
                    {
                        bestCandidate = similarityToPrevious;
                        bestSuccessorIndex = previousIndex;
                        successorDirection = -1;
                    }
                }

                if (i < cutIndexes.Count - 1 || polygonalCut.IsALoop)
                {
                    int nextIndex = (i < cutIndexes.Count - 1) ? i + 1 : 0;
                    int nextVertexIndex = cutIndexes[nextIndex];
                    Vector3 nextVertexDir = meshVertices[uniqueIdToVertexIndex[nextVertexIndex][0]].position -
                                            meshVertices[uniqueIdToVertexIndex[currentIndex][0]].position;
                    nextVertexDir.Normalize();

                    float similarityToNext = Vector3.Dot(previousEdgeDir, nextVertexDir);
                    if (similarityToNext < bestCandidate) // Go to next
                    {
                        bestCandidate = similarityToNext;
                        bestSuccessorIndex = nextIndex;
                        successorDirection = 1;
                    }
                }

            }
        }

        Dictionary<int, int> sharedToUnique = polygonalCut.mesh.sharedVertexLookup;
        if (successorDirection == -1)
        {
            for (int i = bestSuccessorIndex; i > (bestSuccessorIndex - cutIndexes.Count); i--)
            {
                int vertexIndex = uniqueIdToVertexIndex[cutIndexes[(i + cutIndexes.Count) % cutIndexes.Count]][0];
                closure.Add(vertexIndex);
                if(sharedToUnique[vertexIndex] == sharedToUnique[polygonFirstVertex])
                    break;
            }
        }
        else if (successorDirection == 1)
        {
            for (int i = bestSuccessorIndex; i < (bestSuccessorIndex + cutIndexes.Count); i++)
            {
                int vertexIndex = uniqueIdToVertexIndex[cutIndexes[i % cutIndexes.Count]][0];
                closure.Add(vertexIndex);
                if(sharedToUnique[vertexIndex] == sharedToUnique[polygonFirstVertex])
                    break;
            }
        }

        return closure;
    }


    private int[] GetComplementaryPolygons(int[] indexes)
    {
        for (int i = 0; i < indexes.Length; i++)
        {
            for (int j = i+1; j < indexes.Length; j++)
            {
                //Is it the vertex to duplicate?
                if (indexes[i] == indexes[j])
                {
                    int[] complementaryPoly = new int[3];
                    complementaryPoly[0] = indexes[j - 1];
                    complementaryPoly[1] = indexes[j];
                    complementaryPoly[2] = indexes[j + 1];
                    return complementaryPoly;
                }
            }
        }
        return null;
    }

    //Check if the cut is connected to the face edges on its 2 sides
    public bool IsOrphanCut(List<int> cut)
    {
        bool isSinglePoint = cut.Count < 2;
        bool isHeadOrphan = (polygonalCut.m_verticesToAdd[cut[0]].m_Type &
                             (PolygonalCut.VertexType.ExistingVertex | PolygonalCut.VertexType.AddedOnEdge)) == 0;
        bool isTailOrphan = (polygonalCut.m_verticesToAdd[cut[cut.Count - 1]].m_Type &
                             (PolygonalCut.VertexType.ExistingVertex | PolygonalCut.VertexType.AddedOnEdge)) == 0;
        return (isSinglePoint || isHeadOrphan || isTailOrphan);
    }

#endregion

#region Utility functions

        void HandleKeyEvent(Event evt)
        {
            KeyCode key = evt.keyCode;

            switch (key)
            {
                case KeyCode.Backspace:
                {
                    if (m_SelectedIndex >= 0)
                    {
                        UndoUtility.RecordObject(polygonalCut, "Delete Selected Points");
                        polygonalCut.m_verticesToAdd.RemoveAt(m_SelectedIndex);
                        m_SelectedIndex = polygonalCut.m_verticesToAdd.Count - 1;
                        RebuildPolygonalShape(true);
                        evt.Use();
                    }
                    break;
                }

                case KeyCode.Escape:
                {
                    evt.Use();
                    DoPolygonalCut();
                    break;
                }

            }
        }

#endregion

#region GUI Drawing

        void DoExistingPointsGUI()
        {
            Transform trs = polygonalCut.transform;
            int len = polygonalCut.m_verticesToAdd.Count;

            Event evt = Event.current;

            bool used = evt.type == EventType.Used;

            if (!used &&
                (evt.type == EventType.MouseDown &&
                 evt.button == 0 &&
                 !EditorHandleUtility.IsAppendModifier(evt.modifiers)))
            {
                m_SelectedIndex = -1;
                Repaint();
            }

            if (evt.type == EventType.Repaint)
            {
                if (polygonalCut.polygonEditMode == PolygonalCut.PolygonEditMode.Add)
                {
                    for (int index = 0; index < len; index++)
                    {
                        Vector3 point = trs.TransformPoint(polygonalCut.m_verticesToAdd[index].m_Position);
                        float size = HandleUtility.GetHandleSize(point) * k_HandleSize;

                        Handles.color = k_HandleColor;
                        Handles.DotHandleCap(-1, point, Quaternion.identity, size, evt.type);

                        // "clicked" a button
                        if (!used && evt.type == EventType.Used)
                        {
                            used = true;
                        }
                    }

                    if (!m_CurrentPositionToAdd.Equals(Vector3.negativeInfinity))
                    {
                        Vector3 point = trs.TransformPoint(m_CurrentPositionToAdd);
                        //if(m_ModifyPoint && m_SelectedIndex !=0 )
                        if(m_SelectedIndex >= 0 )
                            point = trs.TransformPoint(polygonalCut.m_verticesToAdd[m_SelectedIndex].m_Position);

                        float size = HandleUtility.GetHandleSize(point) * k_HandleSize;

                        Handles.color = m_CurrentHandleColor;

                        Handles.DotHandleCap(-1, point, Quaternion.identity, size, evt.type);
                    }

                    Handles.color = Color.white;
                }
            }
        }

        public void RebuildPolygonalShape(bool vertexCountChanged = false)
        {
            // If Undo is called immediately after creation this situation can occur
            if (polygonalCut == null)
                return;

            DrawPolyLine(polygonalCut.m_verticesToAdd.Select(tup => tup.m_Position).ToList());

            // While the vertex count may not change, the triangle winding might. So unfortunately we can't take
            // advantage of the `vertexCountChanged = false` optimization here.
            ProBuilderEditor.Refresh();
        }

        void DrawPolyLine(List<Vector3> points)
        {
            if (points.Count < 2)
                return;

            int vc = points.Count;

            Vector3[] ver = new Vector3[vc];
            Vector2[] uvs = new Vector2[vc];
            int[] indexes = new int[vc];
            int cnt = points.Count;
            float distance = 0f;

            for (int i = 0; i < vc; i++)
            {
                Vector3 a = points[i % cnt];
                Vector3 b = points[i < 1 ? 0 : i - 1];

                float d = Vector3.Distance(a, b);
                distance += d;

                ver[i] = points[i % cnt];
                uvs[i] = new Vector2(distance, 1f);
                indexes[i] = i;
            }

            m_LineMesh.Clear();
            m_LineMesh.name = "Cut Guide";
            m_LineMesh.vertices = ver;
            m_LineMesh.uv = uvs;
            m_LineMesh.SetIndices(indexes, MeshTopology.LineStrip, 0);
            m_LineMaterial.SetFloat("_LineDistance", distance);

            if (VertexInsertion.ConnectToStartPoint && points.Count > 2)
            {
                Vector3 a = points[vc - 1], b = points[0];

                m_ClosingLineMesh.Clear();
                m_ClosingLineMesh.name = "Cut Closure";
                m_ClosingLineMesh.vertices = new Vector3[]{ a , b };
                m_ClosingLineMesh.uv = new Vector2[]{new Vector2(0,1), Vector2.one };;
                m_ClosingLineMesh.SetIndices(new int[]{0,1}, MeshTopology.LineStrip, 0);
                m_ClosingLineMaterial.SetFloat("_LineDistance", Vector3.Distance(a, b));
            }
            else
            {
                m_ClosingLineMesh.Clear();
                m_ClosingLineMesh.name = "Poly Shape End";
            }

        }

#endregion

    }
}