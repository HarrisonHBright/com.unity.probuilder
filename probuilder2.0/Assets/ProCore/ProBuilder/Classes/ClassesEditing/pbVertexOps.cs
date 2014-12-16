using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using ProBuilder2.Common;
using ProBuilder2.MeshOperations;
using ProBuilder2.Math;
using ProBuilder2.Triangulator;
using ProBuilder2.Triangulator.Geometry;
using System.Linq;

#if PB_DEBUG
using Parabox.Debug;
#endif

namespace ProBuilder2.MeshOperations
{
	public static class pbVertexOps
	{
#region Merge / Split

		/**
		 *	\brief Collapses all passed indices to a single shared index.  Retains vertex normals.
		 *	
		 */
		public static bool MergeVertices(this pb_Object pb, int[] indices, out int collapsedIndex)
		{
			Vector3[] verts = pb.vertices;
			Vector3 cen = Vector3.zero;

			foreach(int i in indices)
				cen += verts[i];
				
			cen /= (float)indices.Length;

			pb_IntArray[] sharedIndices = pb.sharedIndices;
			int newIndex = pb_IntArrayUtility.MergeSharedIndices(ref sharedIndices, indices);
			pb.SetSharedIndices(sharedIndices);

			int firstTriInSharedIndexArr = pb.sharedIndices[newIndex][0];

			pb.SetSharedVertexPosition(firstTriInSharedIndexArr, cen);

			int[] mergedSharedIndex = pb.GetSharedIndices()[newIndex].array;
			
			int[] removedIndices;
			pb.RemoveDegenerateTriangles(out removedIndices);

			// get a non-deleted index to work with
			int ind = -1;
			for(int i = 0; i < mergedSharedIndex.Length; i++)
				if(!removedIndices.Contains(mergedSharedIndex[i]))
					ind = mergedSharedIndex[i];

			int t = ind;
			for(int i = 0; i < removedIndices.Length; i++)
				if(ind > removedIndices[i])	
					t--;

			if(t > -1)
			{
				collapsedIndex = t;
				return true;
			}
			else
			{
				collapsedIndex = -1;
				return false;
			}
		}

		/**
		 *	Similar to Merge vertices, expect that this method only collapses vertices within
		 *	a specified distance of one another (typically epsilon).
		 */
		public static bool WeldVertices(this pb_Object pb, int[] indices, float delta, out int[] welds)
		{
			List<int> universal = new List<int>();
			for(int i = 0; i < indices.Length; i++)
			{
				int index = pb.sharedIndices.IndexOf(indices[i]);

				if(universal.Contains(index))
					continue;
				else
					universal.Add(index);
			}

			int[] groupIndex = pbUtil.FilledArray<int>(-1, indices.Length);
			List<List<int>> groups = new List<List<int>>();
			List<int> used = new List<int>();

			Vector3[] v = pb.vertices;

			for(int i = 0; i < universal.Count-1; i++)
			{
				if(groupIndex[i] > -1)
					continue;

				for(int n = i+1; n < universal.Count; n++)
				{
					if(Vector3.Distance(v[pb.sharedIndices[universal[i]][0]], v[pb.sharedIndices[universal[n]][0]]) < delta)
					{
						if(groupIndex[n] < 0 && groupIndex[i] < 0)
						{ 
							groups.Add( new List<int>() { universal[i], universal[n] } );
							groupIndex[i] = groups.Count-1;
							groupIndex[n] = groups.Count-1;
						}
						else
						{
							if(groupIndex[i] > -1)
							{
								groups[groupIndex[i]].Add(universal[n]);
								groupIndex[n] = groupIndex[i];
							}
							else
							{
								groups[groupIndex[n]].Add(universal[i]);
								groupIndex[i] = groupIndex[n];
							}
						}

						used.Add(universal[i]);
						used.Add(universal[n]);
					}
				}
			}			

			// Rebuild sharedIndices using the new associations
			List<List<int>> sharedIndicesRebuilt = new List<List<int>>();
			welds = new int[groups.Count];

			for(int i = 0; i < groups.Count; i++)
			{
				welds[i] = pb.sharedIndices[groups[i][0]][0];

				sharedIndicesRebuilt.Add( new List<int>(pb.sharedIndices[groups[i][0]].array) );

				for(int n = 1; n < groups[i].Count; n++)
				{
					sharedIndicesRebuilt[i].AddRange( pb.sharedIndices[groups[i][n]].array );
				}

				// Average the vertex positions
				Vector3 avg = pb_Math.Average(pbUtil.ValuesWithIndices(v, sharedIndicesRebuilt[i].ToArray()));

				foreach(int CurTriangle in sharedIndicesRebuilt[i])
				{
					v[CurTriangle] = avg;
				}
			}

			pb.SetVertices(v);

			// Now add in all the unused sharedIndices arrays
			for(int i = 0; i < pb.sharedIndices.Length; i++)
			{
				if(!used.Contains(i))
				{
					sharedIndicesRebuilt.Add( new List<int>(pb.sharedIndices[i].array) );
				}
			}

			pb.SetSharedIndices(sharedIndicesRebuilt.ToPbIntArray());

			return true;	
		}

		/**
		 * Creates separate entries in sharedIndices cache for all passed indices.
		 */
		public static bool SplitVertices(this pb_Object pb, int[] indices)
		{
			pb_IntArray[] sharedIndices = pb.sharedIndices;

			List<int> usedIndex = new List<int>();
			List<int> splits = new List<int>();

			for(int i = 0; i < indices.Length; i++)
			{
				int index = sharedIndices.IndexOf(indices[i]);

				if(!usedIndex.Contains(index))
				{
					usedIndex.Add(index);
					splits.AddRange(sharedIndices[index].array);
				}
			}

			pb_IntArrayUtility.RemoveValues(ref sharedIndices, splits.ToArray());

			foreach(int i in splits)
				pb_IntArrayUtility.AddValueAtIndex(ref sharedIndices, -1, i);

			pb.SetSharedIndices(sharedIndices);

			return true;
		}
#endregion

#region Add / Subtract


	/**
	 *	Given a face and a point, this will add a vertex to the pb_Object and retriangulate the face.
	 */
	public static bool AppendVertexToFace(this pb_Object pb, pb_Face face, Vector3 point, ref pb_Face newFace)
	{
		if(!face.isValid()) return false;

		// First order of business - project face to 2d
		int[] distinctIndices = face.distinctIndices;
		int len = distinctIndices.Length;
		Vector3[] verts = pb.GetVertices(distinctIndices);
		Color[] cols = pbUtil.ValuesWithIndices(pb.colors, distinctIndices);

		// Get the face normal before modifying the vertex array
		Vector3 nrm = pb_Math.Normal(pb.GetVertices(face.indices));
		Vector3 projAxis = pb_Math.ProjectionAxisToVector( pb_Math.VectorToProjectionAxis(nrm) );
		
		// Add the new point
		verts = verts.Add(point);
		cols = cols.Add( pb_Math.Average(cols) );

		// Project
		List<Vector2> plane = new List<Vector2>(pb_Math.PlanarProject(verts, projAxis));

		// Save the sharedIndices index for each distinct vertex
		pb_IntArray[] sharedIndices = pb.sharedIndices;
		int[] sharedIndex = new int[len+1];
		for(int i = 0; i < len; i++)
			sharedIndex[i] = sharedIndices.IndexOf(distinctIndices[i]);
		sharedIndex[len] = -1;	// add the new vertex to it's own sharedIndex

		// Triangulate the face with the new point appended
		int[] tris = Delaunay.Triangulate(plane).ToIntArray();

		// Check to make sure the triangulated face is facing the same direction, and flip if not
		Vector3 del = Vector3.Cross( verts[tris[2]] - verts[tris[0]], verts[tris[1]]-verts[tris[0]]).normalized;
		if(Vector3.Dot(nrm, del) > 0) System.Array.Reverse(tris);

		/**
		 * attempt to figure out where the new UV coordinate should go
		 */
		Vector2[] uvs = new Vector2[len+1];
		System.Array.Copy(pb.GetUVs(distinctIndices), 0, uvs, 0, len);

		pb_Face triangulated_face = new pb_Face(tris, face.material, new pb_UV(face.uv), face.smoothingGroup, face.textureGroup, -1, face.manualUV);

		/**
		 * Attempt to figure out where the new UV point should go
		 */
		// these are the two vertices that are split by the new vertex
		int[] adjacent_vertex = System.Array.FindAll(triangulated_face.edges, x => x.Contains(len)).Select(x => x.x != len ? x.x : x.y).ToArray();

		if(adjacent_vertex.Length == 2)
		{
			uvs[len] = (uvs[adjacent_vertex[0]] + uvs[adjacent_vertex[1]])/2f;
		}
		else
		{
			Debug.LogError("Failed to find appropriate UV coordinate for new vertex point.  Setting face to AutoUV.");
			triangulated_face.manualUV = false;
		}

		// Compose new face
		newFace = pb.AppendFace(verts, cols, uvs, triangulated_face, sharedIndex);

		// And delete the old
		pb.DeleteFace(face);

		return true;
	}

	/**
	 *	Given a face and a point, this will add a vertex to the pb_Object and retriangulate the face.
	 */
	public static bool AppendVerticesToFace(this pb_Object pb, pb_Face face, Vector3[] points, Color[] addColors, out pb_Face newFace)
	{
		if(!face.isValid())
		{
			newFace = face;
			return false;
		}

		// First order of business - project face to 2d
		int[] distinctIndices = face.distinctIndices;
		Vector3[] verts = pb.GetVertices(distinctIndices);
		Color[] cols = pbUtil.ValuesWithIndices(pb.colors, distinctIndices);
		Vector2[] uvs = new Vector2[distinctIndices.Length+points.Length];
		System.Array.Copy(pb.GetUVs(distinctIndices), 0, uvs, 0, distinctIndices.Length);

		// Add the new point
		Vector3[] t_verts = new Vector3[verts.Length + points.Length];
		System.Array.Copy(verts, 0, t_verts, 0, verts.Length);
		System.Array.Copy(points, 0, t_verts, verts.Length, points.Length);
		verts = t_verts;

		// Add the new color
		Color[] t_col = new Color[cols.Length + addColors.Length];
		System.Array.Copy(cols, 0, t_col, 0, cols.Length);
		System.Array.Copy(addColors, 0, t_col, cols.Length, addColors.Length);
		cols = t_col;

		// Get the face normal before modifying the vertex array
		Vector3 nrm = pb_Math.Normal(pb.GetVertices(face.indices));
		Vector3 projAxis = pb_Math.ProjectionAxisToVector( pb_Math.VectorToProjectionAxis(nrm) );
		
		// Project
		List<Vector2> plane = new List<Vector2>(pb_Math.PlanarProject(verts, projAxis));

		// Save the sharedIndices index for each distinct vertex
		pb_IntArray[] sharedIndices = pb.sharedIndices;
		int[] sharedIndex = new int[distinctIndices.Length+points.Length];
		for(int i = 0; i < distinctIndices.Length; i++)
			sharedIndex[i] = sharedIndices.IndexOf(distinctIndices[i]);
		
		for(int i = distinctIndices.Length; i < distinctIndices.Length+points.Length; i++)
			sharedIndex[i] = -1;	// add the new vertex to it's own sharedIndex

		// Triangulate the face with the new point appended
		int[] tris = Delaunay.Triangulate(plane).ToIntArray();
		
		// Check to make sure the triangulated face is facing the same direction, and flip if not
		Vector3 del = Vector3.Cross( verts[tris[2]] - verts[tris[0]], verts[tris[1]]-verts[tris[0]]).normalized;
		if(Vector3.Dot(nrm, del) > 0)
			System.Array.Reverse(tris);
		
		// Build the new face
		pb_Face triangulated_face = new pb_Face(tris, face.material, new pb_UV(face.uv), face.smoothingGroup, face.textureGroup, -1, face.manualUV);

		/**
		 * Attempt to figure out where the new UV point(s) should go
		 */
		for(int n = distinctIndices.Length; n < uvs.Length; n++)
		{
			// these are the two vertices that are split by the new vertex
			int[] adjacent_vertex = System.Array.FindAll(triangulated_face.edges, x => x.Contains(n)).Select(x => x.x != n ? x.x : x.y).ToArray();

			if(adjacent_vertex.Length == 2)
			{
				uvs[n] = (uvs[adjacent_vertex[0]] + uvs[adjacent_vertex[1]])/2f;
			}
			else
			{
				Debug.LogError("Failed to find appropriate UV coordinate for new vertex point.  Setting face to AutoUV.");
				triangulated_face.manualUV = false;
			}
		}

		// Compose new face
		newFace = pb.AppendFace(verts, cols, uvs, triangulated_face, sharedIndex);

		// And delete the old
		pb.DeleteFace(face);	

		return true;
	}

	/**
	 * Removes vertices that no face references.
	 */
	public static int[] RemoveUnusedVertices(this pb_Object pb)
	{
		List<int> del = new List<int>();
		int[] tris = pb_Face.AllTriangles(pb.faces);

		for(int i = 0; i < pb.vertices.Length; i++)
			if(!tris.Contains(i))
				del.Add(i);
		
		pb.DeleteVerticesWithIndices(del.ToArray());
		
		return del.ToArray();
	}

	/**
	 *	Deletes the vertcies from the passed index array.  Handles rebuilding the sharedIndices array.
	 */
	public static void DeleteVerticesWithIndices(this pb_Object pb, int[] distInd)
	{
		Vector3[] verts = pb.vertices;
		Vector2[] uvs = pb.uv;

		verts = verts.RemoveAt(distInd);
		uvs = uvs.RemoveAt(distInd);

		pb_Face[] nFaces = pb.faces;

		// shift all other face indices down to account for moved vertex positions
		for(int i = 0; i < nFaces.Length; i++)
		{
			int[] tris = nFaces[i].indices;
			for(int n = 0; n < tris.Length; n++)
			{
				int sub = 0;
				for(int d = 0; d < distInd.Length; d++)
				{
					if(tris[n] > distInd[d])
						sub++;
				}
				tris[n] -= sub;
			}

			nFaces[i].SetIndices(tris);
		}

		// shift all other face indices in the shared index array down to account for moved vertex positions
		pb_IntArray[] si = pb.sharedIndices;
		pb_IntArrayUtility.RemoveValuesAndShift(ref si, distInd);
		
		pb.SetSharedIndices(si);
		pb.SetVertices(verts);
		pb.SetUV(uvs);

		pb.SetFaces(nFaces);
		pb.RebuildFaceCaches();

		pb.ToMesh();	
	}	
#endregion
	}
}