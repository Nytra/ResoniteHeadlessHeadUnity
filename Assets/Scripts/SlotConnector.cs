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
		public Vector3 Rotation;
		public bool RotationChanged;
		public Vector3 Scale;
		public bool ScaleChanged;
		public ulong RefId;
		public ulong ParentRefId;
		public bool HasParent;
		public bool IsRootSlot;
		public bool Reparent;
		public string SlotName;
		public long WorldId;

		public void Serialize(BinaryWriter bw)
		{
			bw.Write(Active);
			bw.Write(ActiveChanged);

			bw.Write(Position.x);
			bw.Write(Position.y);
			bw.Write(Position.z);
			bw.Write(PositionChanged);

			bw.Write(Rotation.x);
			bw.Write(Rotation.y);
			bw.Write(Rotation.z);
			bw.Write(RotationChanged);

			bw.Write(Scale.x);
			bw.Write(Scale.y);
			bw.Write(Scale.z);
			bw.Write(ScaleChanged);

			bw.Write(RefId);

			bw.Write(ParentRefId);

			bw.Write(HasParent);

			bw.Write(IsRootSlot);

			bw.Write(Reparent);

			bw.Write(SlotName);

			bw.Write(WorldId);
		}
		public void Deserialize(BinaryReader br)
		{
			Active = br.ReadBoolean();
			ActiveChanged = br.ReadBoolean();

			float px = br.ReadSingle();
			float py = br.ReadSingle();
			float pz = br.ReadSingle();
			Position = new Vector3(px, py, pz);
			PositionChanged = br.ReadBoolean();

			float rx = br.ReadSingle();
			float ry = br.ReadSingle();
			float yz = br.ReadSingle();
			Rotation = new Vector3(rx, ry, yz);
			RotationChanged = br.ReadBoolean();

			float sx = br.ReadSingle();
			float sy = br.ReadSingle();
			float sz = br.ReadSingle();
			Scale = new Vector3(sx, sy, sz);
			ScaleChanged = br.ReadBoolean();

			RefId = br.ReadUInt64();

			ParentRefId = br.ReadUInt64();

			HasParent = br.ReadBoolean();

			IsRootSlot = br.ReadBoolean();

			Reparent = br.ReadBoolean();

			SlotName = br.ReadString();

			WorldId = br.ReadInt64();
		}
		public override string ToString()
		{
			return $"ApplyChangesSlotConnector: {Active} {Position} {PositionChanged} {Rotation} {RotationChanged} {Scale} {ScaleChanged} {RefId} {ParentRefId} {HasParent} {IsRootSlot} {Reparent} {SlotName} {WorldId}";
		}
	}

	public class DestroySlotConnector : IUpdatePacket
	{
		public ulong RefID;
		public bool DestroyingWorld;
		public long WorldId;

		public void Serialize(BinaryWriter bw)
		{
			bw.Write(RefID);
			bw.Write(DestroyingWorld);
			bw.Write(WorldId);
		}
		public void Deserialize(BinaryReader br)
		{
			RefID = br.ReadUInt64();
			DestroyingWorld = br.ReadBoolean();
			WorldId = br.ReadInt64();
		}
		public override string ToString()
		{
			return $"DestroySlotConnector: {RefID} {DestroyingWorld} {WorldId}";
		}
	}
}