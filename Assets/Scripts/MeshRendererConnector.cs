using SharedMemory;
using System.Collections.Generic;
using System.Text;

namespace Thundagun
{
	public class MeshRendererConnector
	{
		//public Mesh mesh;
		//public bool nameChanged;
		//public MeshRenderer renderer;
		//public ulong matId;
		//public string shaderPath;
	}
	public class ApplyChangesMeshRendererConnector : IUpdatePacket
	{
		public List<ulong> boneRefIds = new();
		public ulong slotRefId;
		public long worldId;
		public string shaderFilePath;
		public string shaderLocalPath;
		public ulong matCompId;
		public bool isSkinned;
		public string meshPath;
		public ulong meshCompId;
		public List<float> blendShapeWeights = new();

		public int Id => (int)PacketTypes.ApplyChangesMeshRenderer;

		public void Deserialize(CircularBuffer buffer)
		{
			buffer.Read(out slotRefId);
			buffer.Read(out worldId);
			buffer.Read(out isSkinned);

			var bytes2 = new byte[Constants.MAX_STRING_LENGTH];
			buffer.Read(bytes2);
			shaderFilePath = Encoding.UTF8.GetString(bytes2);

			var bytes4 = new byte[Constants.MAX_STRING_LENGTH];
			buffer.Read(bytes4);
			shaderLocalPath = Encoding.UTF8.GetString(bytes4);

			buffer.Read(out matCompId);

			var bytes3 = new byte[Constants.MAX_STRING_LENGTH];
			buffer.Read(bytes3);
			meshPath = Encoding.UTF8.GetString(bytes3);

			buffer.Read(out meshCompId);

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

				int blendShapeWeightCount;
				buffer.Read(out blendShapeWeightCount);
				for (int i = 0; i < blendShapeWeightCount; i++)
				{
					float weight;
					buffer.Read(out weight);
					blendShapeWeights.Add(weight);
				}
			}
		}

		public void Serialize(CircularBuffer buffer)
		{
			buffer.Write(ref slotRefId);
			buffer.Write(ref worldId);
			buffer.Write(ref isSkinned);

			buffer.Write(Encoding.UTF8.GetBytes(shaderFilePath));

			buffer.Write(Encoding.UTF8.GetBytes(shaderLocalPath));

			buffer.Write(ref matCompId);

			buffer.Write(Encoding.UTF8.GetBytes(meshPath));

			buffer.Write(ref meshCompId);

			if (isSkinned)
			{
				int boneRefIdsCount = boneRefIds.Count;
				buffer.Write(ref boneRefIdsCount);
				foreach (var boneRefId in boneRefIds)
				{
					ulong refId = boneRefId;
					buffer.Write(ref refId);
				}

				int blendShapeWeightCount = blendShapeWeights.Count;
				buffer.Write(ref blendShapeWeightCount);
				foreach (var blendShapeWeight in blendShapeWeights)
				{
					float weight = blendShapeWeight;
					buffer.Write(ref weight);
				}
			}
		}
		public override string ToString()
		{
			return $"ApplyChangesMeshRendererConnectorowo: {isSkinned}, {matCompId}, {meshCompId}, {shaderLocalPath}, {meshPath}";
		}
	}

	public class DestroyMeshRendererConnector : IUpdatePacket
	{
		public int Id => (int)PacketTypes.DestroyMeshRenderer;
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