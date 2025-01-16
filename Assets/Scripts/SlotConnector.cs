using SharedMemory;
using System.IO;
using UnityEngine;

namespace Thundagun
{
	public class SlotConnector
	{
		public bool Active;
		public byte ForceLayer;
		public ushort GameObjectRequests;
		public SlotConnector ParentConnector;
		public Vector3 Position;
		public Quaternion Rotation;
		public Vector3 Scale;
		public bool ShouldDestroy;
		public Transform Transform;
		public ulong RefId;
		public WorldConnector WorldConnector;
		//private ulong _lastParent;
		public bool IsRootSlot;

		public GameObject GeneratedGameObject { get; private set; }

		public int Layer => GeneratedGameObject == null ? 0 : GeneratedGameObject.layer;

		public GameObject ForceGetGameObject()
		{
			if (GeneratedGameObject == null)
				GenerateGameObject();
			return GeneratedGameObject;
		}

		public GameObject RequestGameObject()
		{
			GameObjectRequests++;
			return ForceGetGameObject();
		}

		public void FreeGameObject()
		{
			GameObjectRequests--;
			TryDestroy();
		}

		public void TryDestroy(bool destroyingWorld = false)
		{
			if (!ShouldDestroy || GameObjectRequests != 0)
				return;
			if (!destroyingWorld)
			{
				if (GeneratedGameObject != null) UnityEngine.Object.Destroy(GeneratedGameObject);
				ParentConnector?.FreeGameObject();
			}

			GeneratedGameObject = null;
			Transform = null;
			ParentConnector = null;
		}

		private void GenerateGameObject()
		{
			GeneratedGameObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
			Transform = GeneratedGameObject.transform;
			UpdateParent();
			//UpdateLayer();
			SetData();
		}

		public void UpdateParent()
		{
			var gameObject = ParentConnector != null ? ParentConnector.RequestGameObject() : WorldConnector.WorldRoot;
			Transform.SetParent(gameObject.transform, false);

			//if (_lastParent != ParentConnector?.RefId || IsRootSlot)
			//{
			//	_lastParent = ParentConnector?.RefId ?? default;
			//	if (ParentConnector != null)
			//	{
			//		//ParentConnector.FreeGameObject();
			//	}
			//	GameObject gameObject;
			//	if (ParentConnector != null)
			//	{
			//		gameObject = ParentConnector.RequestGameObject();
			//	}
			//	else
			//	{
			//		gameObject = WorldConnector.WorldRoot;
			//	}
			//	Transform.SetParent(gameObject.transform, worldPositionStays: false);
			//}
		}

		public void UpdateLayer()
		{
			var layer = ForceLayer <= 0 ? Transform.parent.gameObject.layer : ForceLayer;
			if (layer == GeneratedGameObject.layer)
				return;
			SetHiearchyLayer(GeneratedGameObject, layer);
		}

		public static void SetHiearchyLayer(GameObject root, int layer)
		{
			root.layer = layer;
			for (var index = 0; index < root.transform.childCount; ++index)
				SetHiearchyLayer(root.transform.GetChild(index).gameObject, layer);
		}

		public void SetData()
		{
			GeneratedGameObject.SetActive(Active);
			var transform = Transform;
			transform.localPosition = Position;
			transform.localRotation = Rotation;
			transform.localScale = Scale;
		}
	}

	public class ApplyChangesSlotConnector : IUpdatePacket
	{
		public bool Active;
		public bool ActiveChanged;
		public Vector3 Position;
		public bool PositionChanged;
		public Quaternion Rotation;
		public bool RotationChanged;
		public Vector3 Scale;
		public bool ScaleChanged;
		public ulong RefId;
		public ulong ParentRefId;
		public bool HasParent;
		public bool IsRootSlot;
		public bool Reparent;
		//public string SlotName;
		public long WorldId;

		public void Serialize(CircularBuffer buffer)
		{
			buffer.Write(ref Active);
			buffer.Write(ref ActiveChanged);

			buffer.Write(ref Position.x);
			buffer.Write(ref Position.y);
			buffer.Write(ref Position.z);
			buffer.Write(ref PositionChanged);

			buffer.Write(ref Rotation.x);
			buffer.Write(ref Rotation.y);
			buffer.Write(ref Rotation.z);
			buffer.Write(ref Rotation.w);
			buffer.Write(ref RotationChanged);

			buffer.Write(ref Scale.x);
			buffer.Write(ref Scale.y);
			buffer.Write(ref Scale.z);
			buffer.Write(ref ScaleChanged);

			buffer.Write(ref RefId);

			buffer.Write(ref ParentRefId);

			buffer.Write(ref HasParent);

			buffer.Write(ref IsRootSlot);

			buffer.Write(ref Reparent);

			//buffer.Write(ref SlotName);

			buffer.Write(ref WorldId);
		}
		public void Deserialize(CircularBuffer buffer)
		{
			buffer.Read(out Active);
			buffer.Read(out ActiveChanged);

			float px, py, pz;
			buffer.Read(out px);
			buffer.Read(out py);
			buffer.Read(out pz);
			Position = new Vector3(px, py, pz);
			buffer.Read(out PositionChanged);

			float rx, ry, rz, rw;
			buffer.Read(out rx);
			buffer.Read(out ry);
			buffer.Read(out rz);
			buffer.Read(out rw);
			Rotation = new Quaternion(rx, ry, rz, rw);
			buffer.Read(out RotationChanged);

			float sx, sy, sz;
			buffer.Read(out sx);
			buffer.Read(out sy);
			buffer.Read(out sz);
			Scale = new Vector3(sx, sy, sz);
			buffer.Read(out ScaleChanged);

			buffer.Read(out RefId);

			buffer.Read(out ParentRefId);

			buffer.Read(out HasParent);

			buffer.Read(out IsRootSlot);

			buffer.Read(out Reparent);

			//SlotName = br.ReadString();

			buffer.Read(out WorldId);
		}

		public override string ToString()
		{
			return $"ApplyChangesSlotConnector: {Active} {Position} {PositionChanged} {Rotation} {RotationChanged} {Scale} {ScaleChanged} {RefId} {ParentRefId} {HasParent} {IsRootSlot} {Reparent} {WorldId}";
		}
	}

	public class DestroySlotConnector : IUpdatePacket
	{
		public ulong RefID;
		public bool DestroyingWorld;
		public long WorldId;

		public void Serialize(CircularBuffer buffer)
		{
			buffer.Write(ref RefID);
			buffer.Write(ref DestroyingWorld);
			buffer.Write(ref WorldId);
		}
		public void Deserialize(CircularBuffer buffer)
		{
			buffer.Read(out RefID);
			buffer.Read(out DestroyingWorld);
			buffer.Read(out WorldId);
		}
		public override string ToString()
		{
			return $"DestroySlotConnector: {RefID} {DestroyingWorld} {WorldId}";
		}
	}
}