using SharedMemory;
using System.IO;
using UnityEngine;
using System.Text;
using UnityEditor;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Linq;
using TMPro;

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
		private SlotConnector _lastParent;
		public bool IsRootSlot;
		public ulong parentId;
		public bool ForceRender;

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

		//public void FreeGameObject()
		//{
		//	GameObjectRequests--;
		//	TryDestroy();
		//}

		//public void TryDestroy(bool destroyingWorld = false)
		//{
		//	if (!ShouldDestroy || GameObjectRequests != 0)
		//		return;
		//	if (!destroyingWorld)
		//	{
		//		if (GeneratedGameObject != null) UnityEngine.Object.Destroy(GeneratedGameObject);
		//		ParentConnector?.FreeGameObject();
		//	}

		//	GeneratedGameObject = null;
		//	Transform = null;
		//	ParentConnector = null;
		//}

		private void GenerateGameObject()
		{
			//GeneratedGameObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
			GeneratedGameObject = GameObject.Instantiate(SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(go => go.name == "Cube"));
			GeneratedGameObject.SetActive(true);
			Transform = GeneratedGameObject.transform;
			UpdateParent();
			//UpdateLayer();
			SetData();
		}

		private SlotConnector GetSlotConnectorById(ulong id)
		{
			if (WorldConnector.refIdToSlot.TryGetValue(id, out var slotConn))
			{
				return slotConn;
			}
			return null;
		}

		public void UpdateParent()
		{
			//var gameObject = ParentConnector != null ? ParentConnector.RequestGameObject() : WorldConnector.WorldRoot;
			//Transform.SetParent(gameObject.transform, false);

			var par = GetSlotConnectorById(parentId);

			if (_lastParent != par || IsRootSlot)
			{
				_lastParent = par;
				if (ParentConnector != null && ParentConnector.GeneratedGameObject != null)
				{
					//ParentConnector.FreeGameObject();

					// this might not be needed?
					//if (ParentConnector.GeneratedGameObject.transform.childCount == 2)
					//{
					//	ParentConnector.ShouldDestroy = true;
					//	ParentConnector.WorldConnector.refIdToSlot.Remove(ParentConnector.RefId);
					//	ParentConnector.WorldConnector.goToSlot.Remove(ParentConnector.GeneratedGameObject);
					//	UnityEngine.Object.Destroy(ParentConnector.GeneratedGameObject);
					//}
				}
				GameObject gameObject;
				if (par != null)
				{
					ParentConnector = par;
					gameObject = ParentConnector.RequestGameObject();
				}
				else
				{
					gameObject = WorldConnector.WorldRoot;
				}
				Transform.SetParent(gameObject.transform, worldPositionStays: false);
			}
		}

		// Update layer currently does not work!
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
			for (var index = 0; index < root.transform.childCount; index++)
				SetHiearchyLayer(root.transform.GetChild(index).gameObject, layer);
		}

		private static void SetGlobalScale(Transform transform, Vector3 globalScale)
		{
			transform.localScale = Vector3.one;
			transform.localScale = new Vector3(globalScale.x / transform.lossyScale.x, globalScale.y / transform.lossyScale.y, globalScale.z / transform.lossyScale.z);
		}

		public void SetData()
		{
			GeneratedGameObject.SetActive(Active);
			var transform = Transform;
			transform.localPosition = Position;
			transform.localRotation = Rotation;
			transform.localScale = Scale;
			SetGlobalScale(GeneratedGameObject.GetComponentInChildren<MeshRenderer>().gameObject.transform, Vector3.one * 0.25f);
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
		public string SlotName;
		public long WorldId;
		public bool IsUserRootSlot;
		public bool HasActiveUser;
		public bool ShouldRender;
		public bool ForceRender;

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

			buffer.Write(Encoding.UTF8.GetBytes(SlotName ?? "NULL"));

			buffer.Write(ref WorldId);

			buffer.Write(ref IsUserRootSlot);

			buffer.Write(ref HasActiveUser);

			buffer.Write(ref ShouldRender);

			buffer.Write(ref ForceRender);
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

			var bytes = new byte[128];
			buffer.Read(bytes);
			SlotName = Encoding.UTF8.GetString(bytes).Trim();

			buffer.Read(out WorldId);

			buffer.Read(out IsUserRootSlot);

			buffer.Read(out HasActiveUser);

			buffer.Read(out ShouldRender);

			buffer.Read(out ForceRender);
		}

		public override string ToString()
		{
			return $"ApplyChangesSlotConnector: {Active} {ActiveChanged} {Position} {PositionChanged} {Rotation} {RotationChanged} {Scale} {ScaleChanged} {RefId} {ParentRefId} {HasParent} {IsRootSlot} {Reparent} SlotName {WorldId} {IsUserRootSlot} {HasActiveUser} {ShouldRender} {ForceRender}";
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