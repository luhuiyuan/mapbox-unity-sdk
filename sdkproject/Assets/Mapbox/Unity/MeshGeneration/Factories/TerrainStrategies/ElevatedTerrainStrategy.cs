using System.Collections.Generic;
using UnityEngine;
using Mapbox.Unity.MeshGeneration.Data;
using Mapbox.Unity.Map;
using Mapbox.Map;
using Mapbox.Utils;
using System;

namespace Mapbox.Unity.MeshGeneration.Factories.TerrainStrategies
{
	public class ElevatedTerrainStrategy : TerrainStrategy, IElevationBasedTerrainStrategy
	{
		Mesh _stitchTarget;

		protected Dictionary<UnwrappedTileId, Mesh> _meshData;
		private MeshData _currentTileMeshData;
		private MeshData _stitchTargetMeshData;

		private Vector3 _newDir;
		private int _vertA, _vertB, _vertC;
		private int _counter;
		private int _sampleCount;
		private Vector3 _sideWallHeight;

		public override void Initialize(ElevationLayerProperties elOptions)
		{
			base.Initialize(elOptions);

			_sampleCount = _elevationOptions.modificationOptions.sampleCount;
			_sideWallHeight = new Vector3(0, -1 * _elevationOptions.sideWallOptions.wallHeight, 0);
			_meshData = new Dictionary<UnwrappedTileId, Mesh>();
			_currentTileMeshData = new MeshData();
			_stitchTargetMeshData = new MeshData();
			var sampleCountSquare = _elevationOptions.modificationOptions.sampleCount * _elevationOptions.modificationOptions.sampleCount;
		}

		public override void RegisterTile(UnityTile tile)
		{
			if (_elevationOptions.unityLayerOptions.addToLayer && tile.gameObject.layer != _elevationOptions.unityLayerOptions.layerId)
			{
				tile.gameObject.layer = _elevationOptions.unityLayerOptions.layerId;
			}

			if (tile.MeshRenderer == null)
			{
				var renderer = tile.gameObject.AddComponent<MeshRenderer>();
				
				//we create side walls as a submesh so addig a second material in that case
				if(_elevationOptions.sideWallOptions.isActive)
				{
					renderer.materials = new Material[2]
					{
						_elevationOptions.requiredOptions.baseMaterial,
						_elevationOptions.sideWallOptions.wallMaterial
					};
				}
				else
				{
					renderer.material = _elevationOptions.requiredOptions.baseMaterial;
				}
			}

			if (tile.MeshFilter == null)
			{
				tile.gameObject.AddComponent<MeshFilter>();
				CreateBaseMesh(tile);
			}

			if (_elevationOptions.requiredOptions.addCollider && tile.Collider == null)
			{
				tile.gameObject.AddComponent<MeshCollider>();
			}

			GenerateTerrainMesh(tile);
		}
		
		public override void UnregisterTile(UnityTile tile)
		{
			_meshData.Remove(tile.UnwrappedTileId);
		}

		#region mesh gen
		private void CreateBaseMesh(UnityTile tile)
		{
			_currentTileMeshData.Clear();
			for (float y = 0; y < _sampleCount; y++)
			{
				var yrat = y / (_sampleCount - 1);
				for (float x = 0; x < _sampleCount; x++)
				{
					var xrat = x / (_sampleCount - 1);

					var xx = Mathd.Lerp(tile.Rect.Min.x, tile.Rect.Max.x, xrat);
					var yy = Mathd.Lerp(tile.Rect.Min.y, tile.Rect.Max.y, yrat);

					_currentTileMeshData.Vertices.Add(new Vector3(
						(float)(xx - tile.Rect.Center.x) * tile.TileScale,
						0,
						(float)(yy - tile.Rect.Center.y) * tile.TileScale));
					_currentTileMeshData.Normals.Add(Mapbox.Unity.Constants.Math.Vector3Up);
					_currentTileMeshData.UV[0].Add(new Vector2(x * 1f / (_sampleCount - 1), 1 - (y * 1f / (_sampleCount - 1))));
				}
			}

			_currentTileMeshData.Triangles.Add(new List<int>());
			int vertA, vertB, vertC;
			for (int y = 0; y < _sampleCount - 1; y++)
			{
				for (int x = 0; x < _sampleCount - 1; x++)
				{
					vertA = (y * _sampleCount) + x;
					vertB = (y * _sampleCount) + x + _sampleCount + 1;
					vertC = (y * _sampleCount) + x + _sampleCount;
					_currentTileMeshData.Triangles[0].Add(vertA);
					_currentTileMeshData.Triangles[0].Add(vertB);
					_currentTileMeshData.Triangles[0].Add(vertC);

					vertA = (y * _sampleCount) + x;
					vertB = (y * _sampleCount) + x + 1;
					vertC = (y * _sampleCount) + x + _sampleCount + 1;
					_currentTileMeshData.Triangles[0].Add(vertA);
					_currentTileMeshData.Triangles[0].Add(vertB);
					_currentTileMeshData.Triangles[0].Add(vertC);
				}
			}
			var mesh = tile.MeshFilter.mesh;
			mesh.SetVertices(_currentTileMeshData.Vertices);
			mesh.SetNormals(_currentTileMeshData.Normals);
			mesh.SetUVs(0, _currentTileMeshData.UV[0]);
			mesh.SetTriangles(_currentTileMeshData.Triangles[0], 0);
			_currentTileMeshData.Clear();
		}

		private void GenerateTerrainMesh(UnityTile tile)
		{
			tile.MeshFilter.mesh.GetVertices(_currentTileMeshData.Vertices);
			tile.MeshFilter.mesh.GetNormals(_currentTileMeshData.Normals);
			tile.MeshFilter.mesh.GetUVs(0, _currentTileMeshData.UV[0]);

			int sideStart = _sampleCount * _sampleCount;

			for (float y = 0; y < _sampleCount; y++)
			{
				for (float x = 0; x < _sampleCount; x++)
				{
					_currentTileMeshData.Vertices[(int)(y * _sampleCount + x)] = new Vector3(
						_currentTileMeshData.Vertices[(int)(y * _sampleCount + x)].x,
						tile.QueryHeightData(x / (_sampleCount - 1), 1 - y / (_sampleCount - 1)),
						_currentTileMeshData.Vertices[(int)(y * _sampleCount + x)].z);
					_currentTileMeshData.Normals[(int)(y * _sampleCount + x)] = Mapbox.Unity.Constants.Math.Vector3Zero;
				}
			}

			for (int y = 0; y < _sampleCount - 1; y++)
			{
				for (int x = 0; x < _sampleCount - 1; x++)
				{
					_vertA = (y * _sampleCount) + x;
					_vertB = (y * _sampleCount) + x + _sampleCount + 1;
					_vertC = (y * _sampleCount) + x + _sampleCount;
					_newDir = Vector3.Cross(_currentTileMeshData.Vertices[_vertB] - _currentTileMeshData.Vertices[_vertA], _currentTileMeshData.Vertices[_vertC] - _currentTileMeshData.Vertices[_vertA]);
					_currentTileMeshData.Normals[_vertA] += _newDir;
					_currentTileMeshData.Normals[_vertB] += _newDir;
					_currentTileMeshData.Normals[_vertC] += _newDir;

					_vertA = (y * _sampleCount) + x;
					_vertB = (y * _sampleCount) + x + 1;
					_vertC = (y * _sampleCount) + x + _sampleCount + 1;
					_newDir = Vector3.Cross(_currentTileMeshData.Vertices[_vertB] - _currentTileMeshData.Vertices[_vertA], _currentTileMeshData.Vertices[_vertC] - _currentTileMeshData.Vertices[_vertA]);
					_currentTileMeshData.Normals[_vertA] += _newDir;
					_currentTileMeshData.Normals[_vertB] += _newDir;
					_currentTileMeshData.Normals[_vertC] += _newDir;
				}
			}

			FixStitches(tile.UnwrappedTileId, _currentTileMeshData);
			if (_elevationOptions.sideWallOptions.isActive)
			{
				_currentTileMeshData.Triangles.Add(new List<int>());
				CreateSides(tile);

				tile.MeshFilter.mesh.SetVertices(_currentTileMeshData.Vertices);
				tile.MeshFilter.mesh.SetNormals(_currentTileMeshData.Normals);
				tile.MeshFilter.mesh.subMeshCount = 2;
				tile.MeshFilter.mesh.SetTriangles(_currentTileMeshData.Triangles[0], 1);
				tile.MeshFilter.mesh.SetUVs(0, _currentTileMeshData.UV[0]);
			}
			else
			{
				tile.MeshFilter.mesh.SetVertices(_currentTileMeshData.Vertices);
				tile.MeshFilter.mesh.SetNormals(_currentTileMeshData.Normals);
			}

			tile.MeshFilter.mesh.RecalculateBounds();

			if (!_meshData.ContainsKey(tile.UnwrappedTileId))
			{
				_meshData.Add(tile.UnwrappedTileId, tile.MeshFilter.mesh);
			}

			if (_elevationOptions.requiredOptions.addCollider)
			{
				var meshCollider = tile.Collider as MeshCollider;
				if (meshCollider)
				{
					meshCollider.sharedMesh = tile.MeshFilter.mesh;
				}
			}
		}

		private void CreateSides(UnityTile tile)
		{
			int index = _currentTileMeshData.Vertices.Count;
			var polyColumnCount = _sampleCount - 1;
			#region North Side Wall
			for (int i = 0; i < _sampleCount - 1; i++)
			{
				//vertices and triangulation goes like;
				//02
				//13

				//first vertex column of the row
				if (i == 0)
				{
					//clone existing terrain vertex, this is the top of the vertex column (0)
					_currentTileMeshData.Vertices.Add(_currentTileMeshData.Vertices[i]);
					//create the bottom vertex (1)
					_currentTileMeshData.Vertices.Add(_currentTileMeshData.Vertices[i] + _sideWallHeight);
					//is using Vector3.forward safe here? we don't rotate or anything so should be good I guess?
					_currentTileMeshData.Normals.Add(Vector3.forward);
					_currentTileMeshData.Normals.Add(Vector3.forward);
					//thinking it like a cube; top north west corner is the 0,1 
					_currentTileMeshData.UV[0].Add(new Vector2(0, 1));
					_currentTileMeshData.UV[0].Add(new Vector2(0, 0));
				}

				//adding next vertex column so we can create triangle inbetween (2)
				_currentTileMeshData.Vertices.Add(_currentTileMeshData.Vertices[i + 1]);
				//next column bottom vertex (3)
				_currentTileMeshData.Vertices.Add(_currentTileMeshData.Vertices[i + 1] + _sideWallHeight);
				_currentTileMeshData.Normals.Add(Vector3.forward);
				_currentTileMeshData.Normals.Add(Vector3.forward);
				//texture is stretched to side wall
				_currentTileMeshData.UV[0].Add(new Vector2((i + 1f) / polyColumnCount, 1));
				_currentTileMeshData.UV[0].Add(new Vector2((i + 1f) / polyColumnCount, 0));

				//0,1,2 triangle ccw
				_currentTileMeshData.Triangles[0].Add(index);
				_currentTileMeshData.Triangles[0].Add(index + 1);
				_currentTileMeshData.Triangles[0].Add(index + 2);

				//2,1,3 triangle ccw
				_currentTileMeshData.Triangles[0].Add(index + 2);
				_currentTileMeshData.Triangles[0].Add(index + 1);
				_currentTileMeshData.Triangles[0].Add(index + 3);

				//move to next vertex column
				index += 2;
			}
			//for the last to vertices of north wall
			index += 2;
			#endregion

			#region West/East walls
			for (int i = 0; i < _sampleCount - 1; i++)
			{
				//first vertex column of the row
				if (i == 0)
				{
					_currentTileMeshData.Vertices.Add(_currentTileMeshData.Vertices[i * _sampleCount]);
					_currentTileMeshData.Vertices.Add(_currentTileMeshData.Vertices[i * _sampleCount] + _sideWallHeight);
					_currentTileMeshData.Normals.Add(Vector3.left);
					_currentTileMeshData.Normals.Add(Vector3.left);
					//thinking it like a cube; top north east is 0.1 for west wall
					_currentTileMeshData.UV[0].Add(new Vector2(0, 1));
					_currentTileMeshData.UV[0].Add(new Vector2(0, 0));

					_currentTileMeshData.Vertices.Add(_currentTileMeshData.Vertices[i * _sampleCount + (_sampleCount - 1)]);
					_currentTileMeshData.Vertices.Add(_currentTileMeshData.Vertices[i * _sampleCount + (_sampleCount - 1)] + _sideWallHeight);
					_currentTileMeshData.Normals.Add(Vector3.right);
					_currentTileMeshData.Normals.Add(Vector3.right);
					//thinking it like a cube; top north east is 0.1 for east wall
					_currentTileMeshData.UV[0].Add(new Vector2(0, 1));
					_currentTileMeshData.UV[0].Add(new Vector2(0, 0));
				}

				_currentTileMeshData.Vertices.Add(_currentTileMeshData.Vertices[(i + 1) * _sampleCount]);
				_currentTileMeshData.Vertices.Add(_currentTileMeshData.Vertices[(i + 1) * _sampleCount] + _sideWallHeight);
				_currentTileMeshData.Normals.Add(Vector3.left);
				_currentTileMeshData.Normals.Add(Vector3.left);
				_currentTileMeshData.UV[0].Add(new Vector2((i + 1f) / polyColumnCount, 1));
				_currentTileMeshData.UV[0].Add(new Vector2((i + 1f) / polyColumnCount, 0));

				_currentTileMeshData.Vertices.Add(_currentTileMeshData.Vertices[(i + 1) * _sampleCount + (_sampleCount - 1)]);
				_currentTileMeshData.Vertices.Add(_currentTileMeshData.Vertices[(i + 1) * _sampleCount + (_sampleCount - 1)] + _sideWallHeight);
				_currentTileMeshData.Normals.Add(Vector3.right);
				_currentTileMeshData.Normals.Add(Vector3.right);
				_currentTileMeshData.UV[0].Add(new Vector2((i + 1f) / polyColumnCount, 1));
				_currentTileMeshData.UV[0].Add(new Vector2((i + 1f) / polyColumnCount, 0));

				//10-----23
				//54-----67

				_currentTileMeshData.Triangles[0].Add(index);
				_currentTileMeshData.Triangles[0].Add(index + 4);
				_currentTileMeshData.Triangles[0].Add(index + 1);

				_currentTileMeshData.Triangles[0].Add(index + 4);
				_currentTileMeshData.Triangles[0].Add(index + 5);
				_currentTileMeshData.Triangles[0].Add(index + 1);

				//----

				_currentTileMeshData.Triangles[0].Add(index + 2);
				_currentTileMeshData.Triangles[0].Add(index + 3);
				_currentTileMeshData.Triangles[0].Add(index + 6);

				_currentTileMeshData.Triangles[0].Add(index + 6);
				_currentTileMeshData.Triangles[0].Add(index + 3);
				_currentTileMeshData.Triangles[0].Add(index + 7);

				//move to next vertex column
				index += 4;
			}
			index += 4;
			#endregion

			#region South Wall
			var cc = _sampleCount * _sampleCount;
			for (int i = cc - _sampleCount; i < cc - 1; i++)
			{
				//first vertex column of the row
				if (i == cc - _sampleCount)
				{
					_currentTileMeshData.Vertices.Add(_currentTileMeshData.Vertices[i]);
					_currentTileMeshData.Vertices.Add(_currentTileMeshData.Vertices[i] + _sideWallHeight);
					_currentTileMeshData.Normals.Add(Vector3.back);
					_currentTileMeshData.Normals.Add(Vector3.back);
					//thinking it like a cube; top south west is 0,1
					_currentTileMeshData.UV[0].Add(new Vector2(0, 1));
					_currentTileMeshData.UV[0].Add(new Vector2(0, 0));
				}

				//adding next vertex column so we can create triangle inbetween
				_currentTileMeshData.Vertices.Add(_currentTileMeshData.Vertices[i + 1]);
				_currentTileMeshData.Vertices.Add(_currentTileMeshData.Vertices[i + 1] + _sideWallHeight);
				_currentTileMeshData.Normals.Add(Vector3.back);
				_currentTileMeshData.Normals.Add(Vector3.back);
				//uv coordinate is a little messy here because i isn't starting at 0
				//cc-i is the progress inversed so we subtract that from _sampleCOunt (edge width in a sense)
				_currentTileMeshData.UV[0].Add(new Vector2((_sampleCount - (cc - i) + 1f) / polyColumnCount, 1));
				_currentTileMeshData.UV[0].Add(new Vector2((_sampleCount - (cc - i) + 1f) / polyColumnCount, 0));

				//02
				//13
				_currentTileMeshData.Triangles[0].Add(index);
				_currentTileMeshData.Triangles[0].Add(index + 2);
				_currentTileMeshData.Triangles[0].Add(index + 1);

				_currentTileMeshData.Triangles[0].Add(index + 2);
				_currentTileMeshData.Triangles[0].Add(index + 3);
				_currentTileMeshData.Triangles[0].Add(index + 1);

				//move to next vertex column
				index += 2;
			}
			#endregion
		}

		/// <summary>
		/// Checkes all neighbours of the given tile and stitches the edges to achieve a smooth mesh surface.
		/// </summary>
		/// <param name="tileId">UnwrappedTileId of the tile being processed.</param>
		/// <param name="mesh"></param>
		private void FixStitches(UnwrappedTileId tileId, MeshData mesh)
		{
			var meshVertCount = _sampleCount * _sampleCount;
			_stitchTarget = null;
			_meshData.TryGetValue(tileId.North, out _stitchTarget);
			if (_stitchTarget != null)
			{
				_stitchTarget.GetVertices(_stitchTargetMeshData.Vertices);
				_stitchTarget.GetNormals(_stitchTargetMeshData.Normals);

				for (int i = 0; i < _sampleCount; i++)
				{
					//just snapping the y because vertex pos is relative and we'll have to do tile pos + vertex pos for x&z otherwise
					mesh.Vertices[i] = new Vector3(
						mesh.Vertices[i].x,
						_stitchTargetMeshData.Vertices[meshVertCount - _sampleCount + i].y,
						mesh.Vertices[i].z);

					mesh.Normals[i] = new Vector3(_stitchTargetMeshData.Normals[meshVertCount - _sampleCount + i].x,
						_stitchTargetMeshData.Normals[meshVertCount - _sampleCount + i].y,
						_stitchTargetMeshData.Normals[meshVertCount - _sampleCount + i].z);
				}
			}

			_stitchTarget = null;
			_meshData.TryGetValue(tileId.South, out _stitchTarget);
			if (_stitchTarget != null)
			{
				_stitchTarget.GetVertices(_stitchTargetMeshData.Vertices);
				_stitchTarget.GetNormals(_stitchTargetMeshData.Normals);

				for (int i = 0; i < _sampleCount; i++)
				{
					mesh.Vertices[meshVertCount - _sampleCount + i] = new Vector3(
						mesh.Vertices[meshVertCount - _sampleCount + i].x,
						_stitchTargetMeshData.Vertices[i].y,
						mesh.Vertices[meshVertCount - _sampleCount + i].z);

					mesh.Normals[meshVertCount - _sampleCount + i] = new Vector3(
						_stitchTargetMeshData.Normals[i].x,
						_stitchTargetMeshData.Normals[i].y,
						_stitchTargetMeshData.Normals[i].z);
				}
			}

			_stitchTarget = null;
			_meshData.TryGetValue(tileId.West, out _stitchTarget);
			if (_stitchTarget != null)
			{
				_stitchTarget.GetVertices(_stitchTargetMeshData.Vertices);
				_stitchTarget.GetNormals(_stitchTargetMeshData.Normals);

				for (int i = 0; i < _sampleCount; i++)
				{
					mesh.Vertices[i * _sampleCount] = new Vector3(
						mesh.Vertices[i * _sampleCount].x,
						_stitchTargetMeshData.Vertices[i * _sampleCount + _sampleCount - 1].y,
						mesh.Vertices[i * _sampleCount].z);

					mesh.Normals[i * _sampleCount] = new Vector3(
						_stitchTargetMeshData.Normals[i * _sampleCount + _sampleCount - 1].x,
						_stitchTargetMeshData.Normals[i * _sampleCount + _sampleCount - 1].y,
						_stitchTargetMeshData.Normals[i * _sampleCount + _sampleCount - 1].z);
				}
			}

			_stitchTarget = null;
			_meshData.TryGetValue(tileId.East, out _stitchTarget);

			if (_stitchTarget != null)
			{
				_stitchTarget.GetVertices(_stitchTargetMeshData.Vertices);
				_stitchTarget.GetNormals(_stitchTargetMeshData.Normals);

				for (int i = 0; i < _sampleCount; i++)
				{
					mesh.Vertices[i * _sampleCount + _sampleCount - 1] = new Vector3(
						mesh.Vertices[i * _sampleCount + _sampleCount - 1].x,
						_stitchTargetMeshData.Vertices[i * _sampleCount].y,
						mesh.Vertices[i * _sampleCount + _sampleCount - 1].z);

					mesh.Normals[i * _sampleCount + _sampleCount - 1] = new Vector3(
						_stitchTargetMeshData.Normals[i * _sampleCount].x,
						_stitchTargetMeshData.Normals[i * _sampleCount].y,
						_stitchTargetMeshData.Normals[i * _sampleCount].z);
				}
			}

			_stitchTarget = null;
			_meshData.TryGetValue(tileId.NorthWest, out _stitchTarget);

			if (_stitchTarget != null)
			{
				_stitchTarget.GetVertices(_stitchTargetMeshData.Vertices);
				_stitchTarget.GetNormals(_stitchTargetMeshData.Normals);

				mesh.Vertices[0] = new Vector3(
					mesh.Vertices[0].x,
					_stitchTargetMeshData.Vertices[meshVertCount - 1].y,
					mesh.Vertices[0].z);

				mesh.Normals[0] = new Vector3(
					_stitchTargetMeshData.Normals[meshVertCount - 1].x,
					_stitchTargetMeshData.Normals[meshVertCount - 1].y,
					_stitchTargetMeshData.Normals[meshVertCount - 1].z);
			}

			_stitchTarget = null;
			_meshData.TryGetValue(tileId.NorthEast, out _stitchTarget);

			if (_stitchTarget != null)
			{
				_stitchTarget.GetVertices(_stitchTargetMeshData.Vertices);
				_stitchTarget.GetNormals(_stitchTargetMeshData.Normals);

				mesh.Vertices[_sampleCount - 1] = new Vector3(
					mesh.Vertices[_sampleCount - 1].x,
					_stitchTargetMeshData.Vertices[meshVertCount - _sampleCount].y,
					mesh.Vertices[_sampleCount - 1].z);

				mesh.Normals[_sampleCount - 1] = new Vector3(
					_stitchTargetMeshData.Normals[meshVertCount - _sampleCount].x,
					_stitchTargetMeshData.Normals[meshVertCount - _sampleCount].y,
					_stitchTargetMeshData.Normals[meshVertCount - _sampleCount].z);
			}

			_stitchTarget = null;
			_meshData.TryGetValue(tileId.SouthWest, out _stitchTarget);

			if (_stitchTarget != null)
			{
				_stitchTarget.GetVertices(_stitchTargetMeshData.Vertices);
				_stitchTarget.GetNormals(_stitchTargetMeshData.Normals);

				mesh.Vertices[meshVertCount - _sampleCount] = new Vector3(
					mesh.Vertices[meshVertCount - _sampleCount].x,
					_stitchTargetMeshData.Vertices[_sampleCount - 1].y,
					mesh.Vertices[meshVertCount - _sampleCount].z);

				mesh.Normals[meshVertCount - _sampleCount] = new Vector3(
					_stitchTargetMeshData.Normals[_sampleCount - 1].x,
					_stitchTargetMeshData.Normals[_sampleCount - 1].y,
					_stitchTargetMeshData.Normals[_sampleCount - 1].z);
			}

			_stitchTarget = null;
			_meshData.TryGetValue(tileId.SouthEast, out _stitchTarget);

			if (_stitchTarget != null)
			{
				_stitchTarget.GetVertices(_stitchTargetMeshData.Vertices);
				_stitchTarget.GetNormals(_stitchTargetMeshData.Normals);

				mesh.Vertices[meshVertCount - 1] = new Vector3(
					mesh.Vertices[meshVertCount - 1].x,
					_stitchTargetMeshData.Vertices[0].y,
					mesh.Vertices[meshVertCount - 1].z);

				mesh.Normals[meshVertCount - 1] = new Vector3(
					_stitchTargetMeshData.Normals[0].x,
					_stitchTargetMeshData.Normals[0].y,
					_stitchTargetMeshData.Normals[0].z);
			}
		}

		private void ResetToFlatMesh(UnityTile tile)
		{
			tile.MeshFilter.mesh.GetVertices(_currentTileMeshData.Vertices);
			tile.MeshFilter.mesh.GetNormals(_currentTileMeshData.Normals);

			_counter = _currentTileMeshData.Vertices.Count;
			for (int i = 0; i < _counter; i++)
			{
				_currentTileMeshData.Vertices[i] = new Vector3(
					_currentTileMeshData.Vertices[i].x,
					0,
					_currentTileMeshData.Vertices[i].z);
				_currentTileMeshData.Normals[i] = Mapbox.Unity.Constants.Math.Vector3Up;
			}

			tile.MeshFilter.mesh.SetVertices(_currentTileMeshData.Vertices);
			tile.MeshFilter.mesh.SetNormals(_currentTileMeshData.Normals);

			tile.MeshFilter.mesh.RecalculateBounds();
		}

		#endregion
	}
}