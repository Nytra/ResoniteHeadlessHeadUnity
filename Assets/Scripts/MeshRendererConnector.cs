using SharedMemory;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Thundagun
{
	public class MeshRendererConnector
	{
		public Mesh mesh;
	}
	public class ApplyChangesMeshRendererConnector : IUpdatePacket
	{
		public List<Vector3> verts = new();
		public List<Vector3> normals = new();
		public List<Vector4> tangents = new();
		public List<Color> colors = new();
		public List<int> triangleIndices = new();
		public ulong slotRefId;
		public long worldId;
		public string shaderPath;

		public void Deserialize(CircularBuffer buffer)
		{
			buffer.Read(out slotRefId);
			buffer.Read(out worldId);

			var bytes2 = new byte[512];
			buffer.Read(bytes2);
			shaderPath = Encoding.UTF8.GetString(bytes2);

			int vertCount;
			buffer.Read(out vertCount);
			for (int i = 0; i < vertCount; i++)
			{
				float x;
				buffer.Read(out x);
				float y;
				buffer.Read(out y);
				float z;
				buffer.Read(out z);
				verts.Add(new Vector3(x, y, z));
			}

			int normalCount;
			buffer.Read(out normalCount);
			for (int i = 0; i < normalCount; i++)
			{
				float x;
				buffer.Read(out x);
				float y;
				buffer.Read(out y);
				float z;
				buffer.Read(out z);
				normals.Add(new Vector3(x, y, z));
			}

			int tangentCount;
			buffer.Read(out tangentCount);
			for (int i = 0; i < tangentCount; i++)
			{
				float x;
				buffer.Read(out x);
				float y;
				buffer.Read(out y);
				float z;
				buffer.Read(out z);
				float w;
				buffer.Read(out w);
				tangents.Add(new Vector4(x, y, z, w));
			}

			int colorCount;
			buffer.Read(out colorCount);
			for (int i = 0; i < colorCount; i++)
			{
				float r;
				buffer.Read(out r);
				float g;
				buffer.Read(out g);
				float b;
				buffer.Read(out b);
				float a;
				buffer.Read(out a);
				colors.Add(new Color(r, g, b, a));
			}

			int triangleIndexCount;
			buffer.Read(out triangleIndexCount);
			for (int i = 0; i < triangleIndexCount / 3; i++)
			{
				int i0;
				buffer.Read(out i0);
				int i1;
				buffer.Read(out i1);
				int i2;
				buffer.Read(out i2);
				triangleIndices.Add(i0);
				triangleIndices.Add(i1);
				triangleIndices.Add(i2);
			}
		}

		public void Serialize(CircularBuffer buffer)
		{
			buffer.Write(ref slotRefId);
			buffer.Write(ref worldId);

			buffer.Write(Encoding.UTF8.GetBytes(shaderPath));

			int vertCount = verts.Count;
			buffer.Write(ref vertCount);
			foreach (var vert in verts)
			{
				float x = vert.x;
				buffer.Write(ref x);
				float y = vert.y;
				buffer.Write(ref y);
				float z = vert.z;
				buffer.Write(ref z);
			}

			int normalCount = normals.Count;
			buffer.Write(ref normalCount);
			foreach (var normal in normals)
			{
				float x = normal.x;
				buffer.Write(ref x);
				float y = normal.y;
				buffer.Write(ref y);
				float z = normal.z;
				buffer.Write(ref z);
			}

			int tangentCount = tangents.Count;
			buffer.Write(ref tangentCount);
			foreach (var tangent in tangents)
			{
				float x = tangent.x;
				buffer.Write(ref x);
				float y = tangent.y;
				buffer.Write(ref y);
				float z = tangent.z;
				buffer.Write(ref z);
				float w = tangent.w;
				buffer.Write(ref w);
			}

			int colorCount = colors.Count;
			buffer.Write(ref colorCount);
			foreach (var color in colors)
			{
				float r = color.r;
				buffer.Write(ref r);
				float g = color.g;
				buffer.Write(ref g);
				float b = color.b;
				buffer.Write(ref b);
				float a = color.a;
				buffer.Write(ref a);
			}

			int triangleIndexCount = triangleIndices.Count;
			buffer.Write(ref triangleIndexCount);
			foreach (var idx in triangleIndices)
			{
				int idx2 = idx;
				buffer.Write(ref idx2);
			}
		}

		public override string ToString()
		{
			return $"ApplyChangesMeshRendererConnector {verts.Count} {normals.Count} {tangents.Count}";
		}
	}

	public class DestroyMeshRendererConnector : IUpdatePacket
	{
		public DestroyMeshRendererConnector(MeshRendererConnector owner)
		{
		}

		public void Deserialize(CircularBuffer buffer)
		{
		}

		public void Serialize(CircularBuffer buffer)
		{
		}
	}
}