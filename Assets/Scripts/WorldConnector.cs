using UnityEngine;
using System.IO;
using System.Collections.Generic;

namespace Thundagun
{
	public enum WorldFocus
	{
		Background,
		Focused,
		Overlay,
		PrivateOverlay
	}

	public class WorldConnector
	{
		public GameObject WorldRoot;
		public Dictionary<ulong, SlotConnector> refIdToSlot = new();
		public Dictionary<GameObject, SlotConnector> goToSlot = new();

		public static void SetLayerRecursively(Transform transform, int layer)
		{
			transform.gameObject.layer = layer;
			for (var index = 0; index < transform.childCount; ++index)
				SetLayerRecursively(transform.GetChild(index), layer);
		}
	}

	public class InitializeWorldConnector : IUpdatePacket
	{
		public long WorldId;
		public void Serialize(BinaryWriter bw)
		{
			bw.Write(WorldId);
		}
		public void Deserialize(BinaryReader br)
		{
			WorldId = br.ReadInt64();
		}
		public override string ToString()
		{
			return $"InitializeWorldConnector: {WorldId}";
		}
	}

	public class ChangeFocusWorldConnector : IUpdatePacket
	{
		public int Focus;
		public long WorldId;

		public void Serialize(BinaryWriter bw)
		{
			bw.Write(Focus);
			bw.Write(WorldId);
		}
		public void Deserialize(BinaryReader br)
		{
			Focus = br.ReadInt32();
			WorldId = br.ReadInt64();
		}
		public override string ToString()
		{
			return $"ChangeFocusWorldConnector: {Focus} {WorldId}";
		}
	}

	public class DestroyWorldConnector : IUpdatePacket
	{
		public long WorldId;
		public void Serialize(BinaryWriter bw)
		{
			bw.Write(WorldId);
		}
		public void Deserialize(BinaryReader br)
		{
			WorldId = br.ReadInt64();
		}
		public override string ToString()
		{
			return $"DestroyWorldConnector: {WorldId}";
		}
	}
}