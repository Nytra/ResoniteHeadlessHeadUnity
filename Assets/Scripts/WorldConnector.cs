using UnityEngine;
using System.IO;
using System.Collections.Generic;
using SharedMemory;

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
			for (var index = 0; index < transform.childCount; index++)
				SetLayerRecursively(transform.GetChild(index), layer);
		}
	}

	public class InitializeWorldConnector : IUpdatePacket
	{
		public long WorldId;
		public void Serialize(CircularBuffer buffer)
		{
			buffer.Write(ref WorldId);
		}
		public void Deserialize(CircularBuffer buffer)
		{
			buffer.Read(out WorldId);
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

		public void Serialize(CircularBuffer buffer)
		{
			buffer.Write(ref Focus);
			buffer.Write(ref WorldId);
		}
		public void Deserialize(CircularBuffer buffer)
		{
			buffer.Read(out Focus);
			buffer.Read(out WorldId);
		}
		public override string ToString()
		{
			return $"ChangeFocusWorldConnector: {Focus} {WorldId}";
		}
	}

	public class DestroyWorldConnector : IUpdatePacket
	{
		public long WorldId;
		public void Serialize(CircularBuffer buffer)
		{
			buffer.Write(ref WorldId);
		}
		public void Deserialize(CircularBuffer buffer)
		{
			buffer.Read(out WorldId);
		}
		public override string ToString()
		{
			return $"DestroyWorldConnector: {WorldId}";
		}
	}
}