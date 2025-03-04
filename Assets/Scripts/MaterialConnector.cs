using SharedMemory;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

namespace Thundagun
{
	public class MaterialConnector
	{
		public Material mat;
		public string shaderFilePath;
		public List<MeshRenderer> renderers = new();
		public Shader shader;
		public ulong ownerId;
		public static HashSet<string> TriggeredLoaded = new();
		public Queue<MaterialAction> mainActionQueue = new();
		public string shaderLocalPath;
		public GameObject proxy;

		public void ProcessQueue()
		{
			// get shader
			//if (AssetManager.FilePathToShader.TryGetValue(shaderPath, out var shadConn))
			//{
				//shader = shadConn.shader;
			//}
			//if (mat is null && shader != null)
			//{
			//	mat = new Material(shader);
			//}
			if (mat != null)
			{
				//if (mat is null)
				//{
				//mat = new Material(shader);
				//}
				//if (mat.shader != shader)
				//{
				//	mat.shader = shader;
				//}

				// do actions

				Queue<MaterialAction> copy;
				lock (mainActionQueue)
				{
					copy = new Queue<MaterialAction>(mainActionQueue);
					mainActionQueue.Clear();
				}
				
				while (copy != null && copy.Count > 0)
				{
					var action = copy.Dequeue();
					switch (action.type)
					{
						case ActionType.Flag:
							mat.SetKeyword(new LocalKeyword(shader, (string)action.obj), action.float4Value.x > 0f);
							break;
						case ActionType.Instancing:
							mat.enableInstancing = action.float4Value.x > 0f;
							break;
						case ActionType.RenderQueue:
							mat.renderQueue = (int)action.float4Value.x;
							break;
						case ActionType.Tag:
							{
								if (action.propertyIndex == 0) // MaterialTag.RenderType
								{
									mat.SetOverrideTag("RenderType", action.obj as string);
									break;
								}
								throw new ArgumentException("Unknown material tag: " + action.propertyIndex);
							}
						case ActionType.Float:
							mat.SetFloat(action.propertyIndex, action.float4Value.x);
							break;
						case ActionType.Float4:
							mat.SetVector(action.propertyIndex, action.float4Value);
							break;
						case ActionType.FloatArray:
							mat.SetFloatArray(action.propertyIndex, (List<float>)action.obj);
							break;
						case ActionType.Float4Array:
							{
								//List<Vector4> list = GetUnityVectorArray(ref action);
								mat.SetVectorArray(action.propertyIndex, (List<Vector4>)action.obj);
								//Pool.Return(ref list);
								break;
							}
						case ActionType.Matrix:
							//matConn.mat.SetMatrix(action.propertyIndex, GetMatrix(ref action).ToUnity()); // aaaaaaaaa
							break;
						case ActionType.Texture:
							//matConn.mat.SetTexture(action.propertyIndex, (action.obj as ITexture)?.GetUnity());
							break;
					}
				}
			}
		}

		public void ApplyChanges(Queue<MaterialAction> actionQueue)
		{
			//Main.myLoggerStatic.PushMessage($"ApplyChangesMaterial. {ownerId}, Actions Count: {actionQueue?.Count ?? -1}, {shaderPath}");

			while (actionQueue != null && actionQueue.Count > 0)
			{
				lock(mainActionQueue)
					mainActionQueue.Enqueue(actionQueue.Dequeue());
			}

			//if (shaderPath == "NULL") return;

			if (shaderFilePath.StartsWith("NULL")) return;

			if (!AssetManager.FilePathToShader.ContainsKey(shaderFilePath))
			{
				if (!TriggeredLoaded.Contains(shaderFilePath))
				{
					TriggeredLoaded.Add(shaderFilePath);
					ShaderConnector.LoadFromFileShader2(shaderFilePath, this, () => ProcessQueue());
				}
				return;
			}

			if (mat is null)
			{
				if (AssetManager.FilePathToShader.TryGetValue(shaderFilePath, out var shadConn))
				{
					Main.myLoggerStatic.PushMessage($"created new mat in apply material code");
					mat = new Material(shadConn.shader);
					shader = shadConn.shader;
					foreach (var rend in renderers)
					{
						rend.sharedMaterial = mat;
					}
				}
				else
				{
					//matConn.mat = new Material(DefaultMat.shader);
					//renderer.sharedMaterial = matConn.mat;
				}
			}
			

			//if (AssetManager.FilePathToShader.TryGetValue(shaderPath, out var shadConn))
			//{
			//shader = shadConn.shader;
			//}

			//if (mat is null && shader != null)
			//{
			//mat = new Material(shader);
			//}

			//if (mat != null && mat.shader != shader)
			//{
			//mat.shader = shader;
			//}

			//if (mat == null)
			//{
			//	Main.myLoggerStatic.PushMessage($"Mat is null");
			//	if (AssetManager.FilePathToShader.TryGetValue(shaderPath, out var shadConn))
			//	{
			//		Main.myLoggerStatic.PushMessage($"Applied new mat with correct shader");
			//		mat = new Material(shadConn.shader);
			//		shader = shadConn.shader;
			//	}
			//}

			//foreach (var renderer in renderers)
			//{
			//	//if (renderer.sharedMaterial.shader == shad.shader) continue;
			//	//Main.myLoggerStatic.PushMessage($"Applying shader retroactively to a renderer with name: {renderer.gameObject.name}");
			//	if (renderer.sharedMaterial != mat)
			//	{
			//		Main.myLoggerStatic.PushMessage($"Fixing wrong mat");
			//		renderer.sharedMaterial = mat;
			//	}

			//	//renderer.sharedMaterial = mat;
			//	//matConn.shader = shad.shader;
			//	//matConn.mat = renderer.sharedMaterial;
			//}

			//if (mat != null)
			//{
			//foreach (var renderer in renderers)
			//{
			//renderer.sharedMaterial = mat;
			//}
			//}

			ProcessQueue();
			
		}
	}
	public enum ActionType
	{
		Flag,
		Tag,
		Float4,
		Float,
		Float4Array,
		FloatArray,
		Matrix,
		Texture,
		RenderQueue,
		Instancing
	}
	public struct MaterialAction
	{
		public ActionType type;

		public int propertyIndex;

		public Vector4 float4Value;

		public object obj;

		public MaterialAction(ActionType type, int propertyIndex, in Vector4 float4Value, object obj = null)
		{
			this.type = type;
			this.propertyIndex = propertyIndex;
			this.float4Value = float4Value;
			this.obj = obj;
		}
	}

	public class ApplyChangesMaterialConnector : IUpdatePacket
	{
		public string shaderFilePath;
		public Queue<MaterialAction> actionQueue;
		public ulong ownerId;
		public string shaderLocalPath;

		public int Id => (int)PacketTypes.ApplyChangesMaterial;
		public void Deserialize(CircularBuffer buffer)
		{
			buffer.ReadString(out shaderFilePath);

			buffer.ReadString(out shaderLocalPath);

			buffer.Read(out ownerId);

			actionQueue = new();
			int actionCount;
			buffer.Read(out actionCount);
			for (int j = 0; j < actionCount; j++)
			{
				MaterialAction action = new();
				// int, int, float4, object

				int type;
				int propertyIndex;
				Vector4 float4Value;
				object obj = null; // string, string, List<float>, List<float4>, itexture - TYPES: flag, tag, floatarray, float4array, texture

				buffer.Read(out type);
				buffer.Read(out propertyIndex);

				// read float4

				float f0, f1, f2, f3;
				buffer.Read(out f0);
				buffer.Read(out f1);
				buffer.Read(out f2);
				buffer.Read(out f3);
				float4Value = new Vector4(f0, f1, f2, f3);

				if (type == (int)ActionType.Flag || type == (int)ActionType.Tag)
				{
					string flagOrTag;
					buffer.ReadString(out flagOrTag);
					obj = flagOrTag;
				}
				else if (type == (int)ActionType.FloatArray)
				{
					List<float> arr = new();
					int arrCount;
					buffer.Read(out arrCount);

					for (int i = 0; i < arrCount; i++)
					{
						float flt;
						buffer.Read(out flt); // add to list
						arr.Add(flt);
					}
					obj = arr;
				}
				else if (type == (int)ActionType.Float4Array)
				{
					List<Vector4> arr = new();
					int arrCount;
					buffer.Read(out arrCount);

					for (int i = 0; i < arrCount; i++)
					{
						float ff0, ff1, ff2, ff3;
						buffer.Read(out ff0);
						buffer.Read(out ff1);
						buffer.Read(out ff2);
						buffer.Read(out ff3);
						arr.Add(new Vector4(ff0, ff1, ff2, ff3));
					}
					obj = arr;
				}
				else if (type == (int)ActionType.Texture)
				{
					// handle textures here later? needs TextureConnector
				}
				action.type = (ActionType)type;
				action.propertyIndex = propertyIndex;
				action.float4Value = float4Value;
				action.obj = obj;
				actionQueue.Enqueue(action);
			}
		}

		public void Serialize(CircularBuffer buffer)
		{
			buffer.WriteString(shaderFilePath);

			buffer.WriteString(shaderLocalPath);

			buffer.Write(ref ownerId);

			int actionCount = actionQueue.Count;
			buffer.Write(ref actionCount);
			while (actionQueue != null && actionQueue.Count > 0)
			{
				MaterialAction action = actionQueue.Dequeue();
				// int, int, float4, object

				int type = (int)action.type;
				int propertyIndex = action.propertyIndex;
				Vector4 float4Value = action.float4Value;
				object obj = action.obj; // string, string, List<float>, List<float4>, itexture - TYPES: flag, tag, floatarray, float4array, texture

				buffer.Write(ref type);
				buffer.Write(ref propertyIndex);

				// write float4

				float f0, f1, f2, f3;
				f0 = float4Value.x;
				f1 = float4Value.y;
				f2 = float4Value.z;
				f3 = float4Value.w;
				buffer.Write(ref f0);
				buffer.Write(ref f1);
				buffer.Write(ref f2);
				buffer.Write(ref f3);

				if (type == (int)ActionType.Flag || type == (int)ActionType.Tag)
				{
					buffer.WriteString((string)obj);
				}
				else if (type == (int)ActionType.FloatArray)
				{
					var arr = (List<float>)obj;
					int arrCount = arr.Count;
					buffer.Write(ref arrCount);

					foreach (var flt in arr)
					{
						float flt2 = flt;
						buffer.Write(ref flt2);
					}
				}
				else if (type == (int)ActionType.Float4Array)
				{
					var arr = (List<Vector4>)obj;
					int arrCount = arr.Count;
					buffer.Write(ref arrCount);

					foreach (var flt in arr)
					{
						float ff0, ff1, ff2, ff3;
						ff0 = flt.x;
						ff1 = flt.y;
						ff2 = flt.z;
						ff3 = flt.w;
						buffer.Write(ref ff0);
						buffer.Write(ref ff1);
						buffer.Write(ref ff2);
						buffer.Write(ref ff3);
					}
				}
				else if (type == (int)ActionType.Texture)
				{
					// handle textures here later? needs TextureConnector
				}
			}
		}

		public override string ToString()
		{
			return $"ApplyChangesMaterialowo. {ownerId}, Actions Count: {actionQueue?.Count ?? -1}, {shaderLocalPath}";
		}
	}

	public class InitializeMaterialPropertiesPacket : IUpdatePacket
	{
		public List<string> PropertyNames;
		public List<int> PropertyIds;

		public int Id => (int)PacketTypes.InitializeMaterialProperties;

		public void Deserialize(CircularBuffer buffer)
		{
			int idCount;
			buffer.Read(out idCount);
			PropertyNames = new();
			for (int i = 0; i < idCount; i++)
			{
				string id;
				buffer.ReadString(out id);
				PropertyNames.Add(id);
			}
		}

		public void Serialize(CircularBuffer buffer)
		{
			int propCount = PropertyIds.Count();
			buffer.Write(ref propCount);
			foreach (var num in PropertyIds)
			{
				int num2 = num;
				buffer.Write(ref num2);
			}
		}
	}
}