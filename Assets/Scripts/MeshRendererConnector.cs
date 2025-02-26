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
	public struct BlendShapeFrame
	{
		public string name;
		public float weight;
		public List<Vector3> positions;
		public List<Vector3> normals;
		public List<Vector3> tangents;
	}
	public class ApplyChangesMeshRendererConnector : IUpdatePacket
	{
		public List<ulong> boneRefIds = new();
		public ulong slotRefId;
		public long worldId;
		public string shaderPath;
		public bool isSkinned;
		public string meshPath;
		//public ulong ownerId;

		public List<Vector3> verts = new();
		public List<Vector3> normals = new();
		public List<Vector4> tangents = new();
		public List<Color> colors = new();
		public List<BoneWeight> boneWeights = new();
		public List<Matrix4x4> bindPoses = new();
		public List<int> triangleIndices = new();
		public List<BlendShapeFrame> blendShapeFrames = new();
		//public Bounds bounds;
		//public string localPath;

		public void Deserialize(CircularBuffer buffer)
		{
			buffer.Read(out slotRefId);
			buffer.Read(out worldId);
			buffer.Read(out isSkinned);

			var bytes2 = new byte[Constants.MAX_STRING_LENGTH];
			buffer.Read(bytes2);
			shaderPath = Encoding.UTF8.GetString(bytes2);

			var bytes3 = new byte[Constants.MAX_STRING_LENGTH];
			buffer.Read(bytes3);
			meshPath = Encoding.UTF8.GetString(bytes3);

			if (isSkinned)
			{
				int boneRefIdsCount;
				buffer.Read(out boneRefIdsCount);
				for (int i = 0; i < boneRefIdsCount; i++)
				{
					ulong refId;
					buffer.Read(out refId);
					boneRefIds.Add(refId);
				}
			}

			//var bytes2 = new byte[Constants.MAX_STRING_LENGTH];
			//buffer.Read(bytes2);
			//localPath = Encoding.UTF8.GetString(bytes2);

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

			int boneBindingCount;
			buffer.Read(out boneBindingCount);
			for (int i = 0; i < boneBindingCount; i++)
			{
				int i0;
				buffer.Read(out i0);
				int i1;
				buffer.Read(out i1);
				int i2;
				buffer.Read(out i2);
				int i3;
				buffer.Read(out i3);

				float w0;
				buffer.Read(out w0);
				float w1;
				buffer.Read(out w1);
				float w2;
				buffer.Read(out w2);
				float w3;
				buffer.Read(out w3);

				var boneWeight = new BoneWeight();
				boneWeight.boneIndex0 = i0;
				boneWeight.boneIndex1 = i1;
				boneWeight.boneIndex2 = i2;
				boneWeight.boneIndex3 = i3;
				boneWeight.weight0 = w0;
				boneWeight.weight1 = w1;
				boneWeight.weight2 = w2;
				boneWeight.weight3 = w3;
				boneWeights.Add(boneWeight);
			}

			int boneCount;
			buffer.Read(out boneCount);
			for (int i = 0; i < boneCount; i++)
			{
				float f0, f1, f2, f3, f4, f5, f6, f7, f8, f9, f10, f11, f12, f13, f14, f15;

				buffer.Read(out f0);
				buffer.Read(out f1);
				buffer.Read(out f2);
				buffer.Read(out f3);

				buffer.Read(out f4);
				buffer.Read(out f5);
				buffer.Read(out f6);
				buffer.Read(out f7);

				buffer.Read(out f8);
				buffer.Read(out f9);
				buffer.Read(out f10);
				buffer.Read(out f11);

				buffer.Read(out f12);
				buffer.Read(out f13);
				buffer.Read(out f14);
				buffer.Read(out f15);
				bindPoses.Add(new Matrix4x4(new Vector4(f0, f4, f8, f12), new Vector4(f1, f5, f9, f13), new Vector4(f2, f6, f10, f14), new Vector4(f3, f7, f11, f15)));
			}

			//float cx, cy, cz;
			//buffer.Read(out cx);
			//buffer.Read(out cy);
			//buffer.Read(out cz);
			//bounds.center = new Vector3(cx, cy, cz);

			//float sx, sy, sz;
			//buffer.Read(out sx);
			//buffer.Read(out sy);
			//buffer.Read(out sz);
			//bounds.size = new Vector3(sx, sy, sz);

			//int blendShapeFrameCount;
			//buffer.Read(out blendShapeFrameCount);
			//for (int i = 0; i < blendShapeFrameCount; i++)
			//{
			//	var frame = new BlendShapeFrame();

			//	string name;
			//	var bytes3 = new byte[Constants.MAX_STRING_LENGTH];
			//	buffer.Read(bytes3);
			//	name = Encoding.UTF8.GetString(bytes3);
			//	frame.name = name;

			//	float weight;
			//	buffer.Read(out weight);
			//	frame.weight = weight;

			//	int positionsCount;
			//	buffer.Read(out positionsCount);
			//	//var positions = new List<Vector3>();
			//	for (int i2 = 0; i2 < positionsCount / 3; i2 ++)
			//	{
			//		float px, py, pz;
			//		buffer.Read(out px);
			//		buffer.Read(out py);
			//		buffer.Read(out pz);
			//		frame.positions.Add(new Vector3(px, py, pz));
			//	}

			//	int normalsCount;
			//	buffer.Read(out normalsCount);
			//	//var normals = new float[normalsCount];
			//	for (int i2 = 0; i2 < normalsCount / 3; i2 ++)
			//	{
			//		float nx, ny, nz;
			//		buffer.Read(out nx);
			//		buffer.Read(out ny);
			//		buffer.Read(out nz);
			//		frame.normals.Add(new Vector3(nx, ny, nz));
			//	}

			//	int tangentsCount;
			//	buffer.Read(out tangentsCount);
			//	//var tangents = new float[tangentsCount];
			//	for (int i2 = 0; i2 < tangentsCount / 3; i2 ++)
			//	{
			//		float tx, ty, tz;
			//		buffer.Read(out tx);
			//		buffer.Read(out ty);
			//		buffer.Read(out tz);
			//		frame.tangents.Add(new Vector3(tx, ty, tz));
			//	}
			//}
		}

		public void Serialize(CircularBuffer buffer)
		{
			buffer.Write(ref slotRefId);
			buffer.Write(ref worldId);
			buffer.Write(ref isSkinned);

			buffer.Write(Encoding.UTF8.GetBytes(shaderPath));

			buffer.Write(Encoding.UTF8.GetBytes(meshPath));

			if (isSkinned)
			{
				int boneRefIdsCount = boneRefIds.Count;
				buffer.Write(ref boneRefIdsCount);
				foreach (var boneRefId in boneRefIds)
				{
					ulong refId = boneRefId;
					buffer.Write(ref refId);
				}
			}

			//buffer.Write(Encoding.UTF8.GetBytes(localPath));

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

			int boneBindingCount = boneWeights.Count;
			buffer.Write(ref boneBindingCount);
			foreach (var boneBinding in boneWeights)
			{
				int i0 = boneBinding.boneIndex0;
				buffer.Write(ref i0);
				int i1 = boneBinding.boneIndex1;
				buffer.Write(ref i1);
				int i2 = boneBinding.boneIndex2;
				buffer.Write(ref i2);
				int i3 = boneBinding.boneIndex3;
				buffer.Write(ref i3);

				float w0 = boneBinding.weight0;
				buffer.Write(ref w0);
				float w1 = boneBinding.weight1;
				buffer.Write(ref w1);
				float w2 = boneBinding.weight2;
				buffer.Write(ref w2);
				float w3 = boneBinding.weight3;
				buffer.Write(ref w3);
			}

			int bindPoseCount = bindPoses.Count;
			buffer.Write(ref bindPoseCount);
			foreach (var bindPose in bindPoses)
			{
				float f0, f1, f2, f3, f4, f5, f6, f7, f8, f9, f10, f11, f12, f13, f14, f15;

				f0 = bindPose.m00;
				buffer.Write(ref f0);
				f1 = bindPose.m01;
				buffer.Write(ref f1);
				f2 = bindPose.m02;
				buffer.Write(ref f2);
				f3 = bindPose.m03;
				buffer.Write(ref f3);

				f4 = bindPose.m10;
				buffer.Write(ref f4);
				f5 = bindPose.m11;
				buffer.Write(ref f5);
				f6 = bindPose.m12;
				buffer.Write(ref f6);
				f7 = bindPose.m13;
				buffer.Write(ref f7);

				f8 = bindPose.m20;
				buffer.Write(ref f8);
				f9 = bindPose.m21;
				buffer.Write(ref f9);
				f10 = bindPose.m22;
				buffer.Write(ref f10);
				f11 = bindPose.m23;
				buffer.Write(ref f11);

				f12 = bindPose.m30;
				buffer.Write(ref f12);
				f13 = bindPose.m31;
				buffer.Write(ref f13);
				f14 = bindPose.m32;
				buffer.Write(ref f14);
				f15 = bindPose.m33;
				buffer.Write(ref f15);
			}

			//float cx, cy, cz;
			//cx = bounds.center.x;
			//cy = bounds.center.y;
			//cz = bounds.center.z;
			//buffer.Write(ref cx);
			//buffer.Write(ref cy);
			//buffer.Write(ref cz);

			//float sx, sy, sz;
			//sx = bounds.size.x;
			//sy = bounds.size.y;
			//sz = bounds.size.z;
			//buffer.Write(ref sx);
			//buffer.Write(ref sy);
			//buffer.Write(ref sz);

			//int blendShapeFrameCount = blendShapeFrames.Count;
			//buffer.Write(ref blendShapeFrameCount);
			//foreach (var blendShapeFrame in blendShapeFrames)
			//{
			//	string name = blendShapeFrame.name;
			//	name = name.Substring(0, Math.Min(name.Length, Constants.MAX_STRING_LENGTH));
			//	buffer.Write(Encoding.UTF8.GetBytes(name));

			//	float weight = blendShapeFrame.weight;
			//	buffer.Write(ref weight);

			//	int positionsCount = blendShapeFrame.positions.Count;
			//	buffer.Write(ref positionsCount);
			//	//var positions = new float[positionsCount];

			//	foreach (var pos in blendShapeFrame.positions)
			//	{
			//		float px = pos.x;
			//		float py = pos.y;
			//		float pz = pos.z;
			//		buffer.Write(ref px);
			//		buffer.Write(ref py);
			//		buffer.Write(ref pz);
			//	}

			//	int normalsCount = blendShapeFrame.normals.Count;
			//	buffer.Write(ref normalsCount);
			//	//var normals = new float[normalsCount];

			//	foreach (var norm in blendShapeFrame.normals)
			//	{
			//		float nx = norm.x;
			//		float ny = norm.y;
			//		float nz = norm.z;
			//		buffer.Write(ref nx);
			//		buffer.Write(ref ny);
			//		buffer.Write(ref nz);
			//	}

			//	int tangentsCount = blendShapeFrame.tangents.Count;
			//	buffer.Write(ref tangentsCount);
			//	//var tangents = new float[tangentsCount];

			//	foreach (var tang in blendShapeFrame.tangents)
			//	{
			//		float tx = tang.x;
			//		float ty = tang.y;
			//		float tz = tang.z;
			//		buffer.Write(ref tx);
			//		buffer.Write(ref ty);
			//		buffer.Write(ref tz);
			//	}
			//}
		}
		public override string ToString()
		{
			return $"ApplyChangesMeshRendererConnector: {isSkinned} {shaderPath} {meshPath}";
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