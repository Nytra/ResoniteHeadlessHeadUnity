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
		public List<ulong> boneRefIds = new();
		public ulong slotRefId;
		public long worldId;
		public string shaderPath;
		public bool isSkinned;
		public string meshPath;

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