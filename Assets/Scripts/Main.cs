using UnityEngine;
using System;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Collections.Generic;
using SharedMemory;
using TMPro;
using UnityEditor;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace Thundagun
{
	public class Constants
	{
		public const int MAX_STRING_LENGTH = 256;
	}
	public class WorldManager
	{
		public static Dictionary<long, WorldConnector> idToWorld = new();
		public static Dictionary<GameObject, WorldConnector> goToWorld = new();
	}

	public class AssetManager
	{
		public static Dictionary<string, ShaderConnector> FilePathToShader = new();
		public static Dictionary<ulong, MeshConnector> OwnerIdToMesh = new();
		public static Dictionary<string, MeshConnector> LocalPathToMesh = new();
		public static Dictionary<ulong, MaterialConnector> OwnerIdToMaterial = new();
		public static Dictionary<MeshRenderer, MeshRendererConnector> Renderers = new();
		//public static Dictionary<ulong, MeshRendererConnector> OwnerIdToMeshRenderer = new();
		//public static Dictionary<string, MaterialConnector> LocalPathToMaterial = new(); // just for testing
	}
	public struct PacketStruct
	{
		public IUpdatePacket packet;
		public Action callback;
	}
	//public class ReturnController
	//{
		
	//}

	public enum PacketTypes
	{
		None, // 0 means no packet
		ApplyChangesSlot,
		DestroySlot,
		InitializeWorld,
		ChangeFocusWorld,
		DestroyWorld,
		ApplyChangesMeshRenderer,
		DestroyMeshRenderer,
		LoadFromFileShader,
		ApplyChangesMesh,
		ApplyChangesMaterial,
		InitializeMaterialProperties,
		ShaderLoadedCallback
	}

	public class Main : MonoBehaviour
	{
		public MyLogger myLogger;
		public GameObject worldsRoot;
		public GameObject camera1;
		public Material DefaultMat;
		//public GameObject emptyGameObject;
		public GameObject shadersRoot;
		public GameObject matsRoot;
		public List<int> testList;
		public float moveSpeed;
		public float camSpeed;
		private static bool started = false;
		private static CircularBuffer buffer;

		//

		public static CircularBuffer returnBuffer;
		public static Queue<PacketStruct> packets = new();

		//

		public static MyLogger myLoggerStatic;
		public static GameObject shadersRootStatic;
		public static GameObject matsRootStatic;

		private static BufferReadWrite syncBuffer;
		private static Queue<Action> synchronousActions = new();

		// Track the current vertical angle
		private float currentVerticalAngle = 0f;
		private float verticalAngleLimit = 80f; // Limit in degrees, adjust as needed

		//private static int updates = 0;
		const bool DEBUG = true; // use when running in the Unity editor, so that the editor doesn't get closed

		public static void QueuePacket(IUpdatePacket packet, Action callback = null)
		{
			//UniLog.Log(packet.ToString());
			var packetStruct = new PacketStruct();
			packetStruct.packet = packet;
			packetStruct.callback = callback;
			lock (packets)
			{
				packets.Enqueue(packetStruct);
			}
		}
		public void ReturnTask()
		{
			while (true)
			{
				try
				{
					if (packets.Count > 0)
					{
						Queue<PacketStruct> copy;
						lock (packets)
						{
							copy = new Queue<PacketStruct>(packets);
							packets.Clear();
						}
						while (copy.Count > 0)
						{
							var packetStruct = copy.Dequeue();
							var num = packetStruct.packet.Id;
							returnBuffer.Write(ref num);
							try
							{
								packetStruct.packet.Serialize(returnBuffer);
							}
							catch (Exception e)
							{
								myLogger.PushMessage($"Exception during serialization: {e}");
								throw;
							}
							try
							{
								packetStruct.callback?.Invoke();
							}
							catch (Exception e)
							{
								myLogger.PushMessage($"Exception running packet queue callback: {e}");
								throw;
							}
						}
					}
				}
				catch (Exception e)
				{
					myLogger.PushMessage($"Exception running packet task: {e}");
					throw;
				}
				//int n;
				//returnBuffer.Read(out n); // halt until the client sends data in this buffer
			}
		}

		void OnApplicationQuit()
		{
			buffer.Close();
			returnBuffer.Close();
		}

		// Start is called once before the first execution of Update after the MonoBehaviour is created
		void Start()
		{
		}

		// Update is called once per frame
		void Update()
		{
			if (!started && myLogger != null)
			{
				started = true;

				myLoggerStatic = myLogger;
				shadersRootStatic = shadersRoot;
				matsRootStatic = matsRoot;

				var args = Environment.GetCommandLineArgs();
				if (args != null)
				{
					myLogger.PushMessage(string.Join(',', args));
				}

				try
				{
					syncBuffer = new BufferReadWrite($"SyncBuffer{DateTime.Now.Minute}");
				}
				catch (Exception e)
				{
					try
					{
						int min = DateTime.Now.Minute;
						if (min - 1 < 0) min = 59;
						else min -= 1;
						syncBuffer = new BufferReadWrite($"SyncBuffer{min}"); // try connecting to the previous minute's syncbuffer, in case the client opened late
					}
					catch (Exception ex)
					{
						myLogger.PushMessage("Could not open sync buffer.");
					}
				}

				myLogger.PushMessage("SyncBuffer opened.");

				int num;

				myLogger.PushMessage("Waiting for SyncBuffer message...");

				syncBuffer.Read(out num);

				myLogger.PushMessage($"Got id {num} from SyncBuffer.");

				buffer = new CircularBuffer($"MyBuffer{num}");

				returnBuffer = new CircularBuffer($"ReturnBuffer{num}");

				myLogger.PushMessage($"MyBuffer{num} opened.");

				myLogger.PushMessage($"Returning id {num}.");

				returnBuffer.Write(ref num);

				myLogger.PushMessage("Starting message loop!!!");

				num = 0;

				syncBuffer.Close();

				Task.Run(ReturnTask);

				Task.Run(async () =>
				{
					while (true)
					{
						try
						{
							buffer.Read(out num);

							if (num != 0)
							{
								if (num == (int)PacketTypes.ApplyChangesSlot)
								{
									ApplyChangesSlotConnector deserializedObject = new();
									deserializedObject.Deserialize(buffer);

									RunSynchronously(() =>
									{
										//myLogger.PushMessage(deserializedObject.ToString());

										if (WorldManager.idToWorld.TryGetValue(deserializedObject.WorldId, out var world))
										{
											if (world.refIdToSlot.TryGetValue(deserializedObject.RefId, out var slotConn))
											{
												slotConn.IsRootSlot = deserializedObject.IsRootSlot;
												slotConn.Active = deserializedObject.Active;
												slotConn.Position = deserializedObject.Position;
												slotConn.Rotation = deserializedObject.Rotation;
												slotConn.Scale = deserializedObject.Scale;
												slotConn.RefId = deserializedObject.RefId;
												slotConn.IsLocalElement = deserializedObject.IsLocalElement;

												if (deserializedObject.ForceRender)
													slotConn.ForceRender = true;

												var generatedGameObject = slotConn.GeneratedGameObject;
												if (generatedGameObject == null)
													return;

												if (world.refIdToSlot.TryGetValue(deserializedObject.ParentRefId, out var parentSlot))
												{
													if (parentSlot != slotConn.ParentConnector)
													{
														slotConn.parentId = deserializedObject.ParentRefId;
														slotConn.UpdateParent();
													}
												}

												//slotConn.UpdateLayer();
												slotConn.SetData();
												slotConn.GeneratedGameObject.name = deserializedObject.SlotName;

												if (slotConn.GeneratedGameObject.name == "LocalAssets" && slotConn.ParentConnector.IsRootSlot && slotConn.IsLocalElement)
												{
													slotConn.GeneratedGameObject.SetActive(false);
												}

												var text = slotConn.GeneratedGameObject.GetComponentInChildren<TextMeshPro>(includeInactive: true);
												if ((deserializedObject.ShouldRender || slotConn.ForceRender || deserializedObject.IsUserRootSlot || deserializedObject.IsRootSlot) && deserializedObject.Active)
												{
													text.text = deserializedObject.SlotName;
													text.gameObject.SetActive(true);
												}
												else
												{
													text.text = "";
												}

												if (deserializedObject.HasActiveUser)
												{
													//slotConn.GeneratedGameObject.transform.GetChild(0).GetComponentInChildren<MeshRenderer>().material.color = Color.green;
													slotConn.GeneratedGameObject.GetComponentInChildren<TextMeshPro>(includeInactive: true).color = Color.green;
												}
												else
												{
													//slotConn.GeneratedGameObject.transform.GetChild(0).GetComponentInChildren<MeshRenderer>().material.color = Color.white;
													slotConn.GeneratedGameObject.GetComponentInChildren<TextMeshPro>(includeInactive: true).color = Color.white;
												}
												

												//slotConn.GeneratedGameObject.transform.GetChild(0).GetComponentInChildren<MeshRenderer>().enabled = (deserializedObject.IsUserRootSlot) && deserializedObject.Active;
											}
											else
											{
												// GenerateGameObject
												var newSc = new SlotConnector();

												if (WorldManager.idToWorld.TryGetValue(deserializedObject.WorldId, out var world2))
												{
													newSc.WorldConnector = world2;
												}
												if (world2.refIdToSlot.TryGetValue(deserializedObject.ParentRefId, out var parentSlot))
												{
													newSc.ParentConnector = parentSlot;
												}

												newSc.IsRootSlot = deserializedObject.IsRootSlot;
												newSc.Active = deserializedObject.Active;
												newSc.Position = deserializedObject.Position;
												newSc.Rotation = deserializedObject.Rotation;
												newSc.Scale = deserializedObject.Scale;
												newSc.RefId = deserializedObject.RefId;
												newSc.IsLocalElement = deserializedObject.IsLocalElement;

												newSc.parentId = deserializedObject.ParentRefId;

												if (deserializedObject.ForceRender)
													newSc.ForceRender = true;

												var go = newSc.RequestGameObject();
												go.name = deserializedObject.SlotName;

												if (go.name == "LocalAssets" && newSc.ParentConnector.IsRootSlot && newSc.IsLocalElement)
												{
													go.SetActive(false);
												}

												var text = go.GetComponentInChildren<TextMeshPro>(includeInactive: true);
												if ((deserializedObject.ShouldRender || newSc.ForceRender || deserializedObject.IsUserRootSlot || deserializedObject.IsRootSlot) && deserializedObject.Active)
												{
													text.text = deserializedObject.SlotName;
													text.gameObject.SetActive(true);
												}
												else
												{
													text.text = "";
												}

												if (deserializedObject.HasActiveUser)
												{
													//go.transform.GetChild(0).GetComponentInChildren<MeshRenderer>().material.color = Color.green;
													go.GetComponentInChildren<TextMeshPro>(includeInactive: true).color = Color.green;
												}

												//go.transform.GetChild(0).GetComponentInChildren<MeshRenderer>().enabled = (deserializedObject.IsUserRootSlot) && deserializedObject.Active;

												world2.refIdToSlot.Add(deserializedObject.RefId, newSc);
												world2.goToSlot.Add(go, newSc);
											}
										}
										else
										{
											myLogger.PushMessage("No world in ApplyChangesSlotConnector!");
										}
									});
								}
								else if (num == (int)PacketTypes.InitializeWorld)
								{
									InitializeWorldConnector deserializedObject = new();
									deserializedObject.Deserialize(buffer);

									RunSynchronously(() =>
									{
										myLogger.PushMessage(deserializedObject.ToString());

										var world = new WorldConnector();
										world.WorldRoot = new GameObject("World" + deserializedObject.WorldId.ToString());
										world.WorldRoot.SetActive(false);
										world.WorldRoot.transform.SetParent(worldsRoot.transform);
										world.WorldRoot.transform.position = Vector3.zero;
										world.WorldRoot.transform.rotation = Quaternion.identity;
										world.WorldRoot.transform.localScale = Vector3.one;
										WorldManager.idToWorld.Add(deserializedObject.WorldId, world);
										WorldManager.goToWorld.Add(world.WorldRoot, world);
									});
								}
								else if (num == (int)PacketTypes.DestroySlot)
								{
									DestroySlotConnector deserializedObject = new();
									deserializedObject.Deserialize(buffer);

									RunSynchronously(() =>
									{
										//myLogger.PushMessage(deserializedObject.ToString());

										if (WorldManager.idToWorld.TryGetValue(deserializedObject.WorldId, out var world))
										{
											if (world.refIdToSlot.TryGetValue(deserializedObject.RefID, out var slot))
											{
												slot.ShouldDestroy = true;
												world.refIdToSlot.Remove(deserializedObject.RefID);
												if (slot.GeneratedGameObject != null)
												{
													world.goToSlot.Remove(slot.GeneratedGameObject);
												}
												//go.TryDestroy(deserializedObject.DestroyingWorld);
												UnityEngine.Object.Destroy(slot.GeneratedGameObject);
											}
											else
											{
												myLogger.PushMessage("No slot in DestroySlotConnector!");
											}
										}
										else
										{
											// This kind of gets hit a lot
											myLogger.PushMessage("No world in DestroySlotConnector!");
										}
									});
								}
								else if (num == (int)PacketTypes.ChangeFocusWorld)
								{
									ChangeFocusWorldConnector deserializedObject = new();
									deserializedObject.Deserialize(buffer);

									RunSynchronously(() =>
									{
										myLogger.PushMessage(deserializedObject.ToString());
										var focus = (WorldFocus)deserializedObject.Focus;
										if (WorldManager.idToWorld.TryGetValue(deserializedObject.WorldId, out var world))
										{
											switch (focus)
											{
												case WorldFocus.Background:
													world.WorldRoot.SetActive(false);
													break;
												case WorldFocus.Focused:
												case WorldFocus.Overlay:
													world.WorldRoot.SetActive(true);
													break;
												case WorldFocus.PrivateOverlay:
													world.WorldRoot.SetActive(true);
													//WorldConnector.SetLayerRecursively(world.WorldRoot.transform, LayerMask.NameToLayer("Private"));
													break;
											}
										}
										else
										{
											myLogger.PushMessage("No world in ChangeFocusWorldConnector!");
										}
									});
								}
								else if (num == (int)PacketTypes.DestroyWorld)
								{
									DestroyWorldConnector deserializedObject = new();
									deserializedObject.Deserialize(buffer);

									RunSynchronously(() => 
									{
										if (WorldManager.idToWorld.TryGetValue(deserializedObject.WorldId, out var world))
										{
											if (world.WorldRoot) UnityEngine.Object.Destroy(world.WorldRoot);
											world.WorldRoot = null;
										}
									});
								}
								else if (num == (int)PacketTypes.ApplyChangesMeshRenderer)
								{
									ApplyChangesMeshRendererConnector deserializedObject = new();
									deserializedObject.Deserialize(buffer);

									RunSynchronously(() => 
									{
										//myLogger.PushMessage(deserializedObject.ToString());
										//if (deserializedObject.meshPath.Trim() == "NULL") return;
										if (WorldManager.idToWorld.TryGetValue(deserializedObject.worldId, out var world))
										{
											if (world.refIdToSlot.TryGetValue(deserializedObject.slotRefId, out var slot))
											{
												var go = slot.ForceGetGameObject();

												if (!deserializedObject.isSkinned)
												{
													MeshFilter filter = null;
													filter = go.GetComponent<MeshFilter>();
													if (filter == null)
													{
														filter = go.AddComponent<MeshFilter>();
													}
													MeshRenderer renderer = go.GetComponent<MeshRenderer>();
													if (renderer == null)
													{
														renderer = go.AddComponent<MeshRenderer>();
													}

													//MeshRendererConnector rendConn;
													//if (!AssetManager.Renderers.TryGetValue(renderer, out rendConn))
													//{
														//rendConn = new();
														//AssetManager.Renderers.Add(renderer, rendConn);
													//}
													//rendConn.renderer = renderer;
													//rendConn.matId = deserializedObject.matCompId;
													//rendConn.shaderPath = deserializedObject.shaderPath;

													renderer.sharedMaterial = null;//DefaultMat;
													renderer.enabled = true;

													//myLogger.PushMessage($"MeshRenderer needs mat: {deserializedObject.matCompId} {deserializedObject.shaderPath}");

													renderer.gameObject.name = " - MatId: " + deserializedObject.matCompId.ToString();

													//MaterialConnector matConnMain = null;

													if (deserializedObject.matCompId != default)
													{
														if (AssetManager.OwnerIdToMaterial.TryGetValue(deserializedObject.matCompId, out MaterialConnector matConn))
														{
															//matConnMain = matConn;
															if (matConn.mat == null)
															{
																//if (AssetManager.FilePathToShader.TryGetValue(deserializedObject.shaderFilePath, out var shadConn))
																//{
																//	myLogger.PushMessage($"created new mat in mesh renderer code");
																//	matConn.mat = new Material(shadConn.shader);
																//	matConn.shader = shadConn.shader;
																//	renderer.sharedMaterial = matConn.mat;
																//}
																//else
																//{
																//	//matConn.mat = new Material(DefaultMat.shader);
																//	//renderer.sharedMaterial = matConn.mat;
																//}
																myLogger.PushMessage($"MeshRenderer found mat but it was null {deserializedObject.matCompId}");
															}
															else
															{
																//if (matConn.mat.shader != matConn.shader)
																//{
																//matConn.mat.shader = matConn.shader;
																//}
																renderer.sharedMaterial = matConn.mat;
																//myLogger.PushMessage($"MeshRenderer chose existing mat {deserializedObject.matCompId}");
															}

															if (!matConn.renderers.Contains(renderer))
															{
																matConn.renderers.Add(renderer);
																if (matConn.proxy != null)
																{
																	var proxy = matConn.proxy.GetComponent<MatDebug>();
																	proxy.TheList.Add(renderer);
																}
															}

															matConn.ownerId = deserializedObject.matCompId;
															if (matConn.shaderFilePath != "NULL")
																matConn.shaderFilePath = deserializedObject.shaderFilePath;
															if (matConn.shaderLocalPath != "NULL")
																matConn.shaderLocalPath = deserializedObject.shaderLocalPath;

															if (matConn.proxy != null)
																matConn.proxy.name = $"Mat: {deserializedObject.matCompId}, {deserializedObject.shaderFilePath}, {deserializedObject.shaderLocalPath}";

															matConn.ApplyChanges(new Queue<MaterialAction>());

															//myLogger.PushMessage($"MeshRenderer found existing mat: {deserializedObject.matId}");
														}
														else
														{
															// here

															//MaterialConnector matConn2 = new();
															////matConnMain = matConn2;
															//matConn2.renderers.Add(renderer);
															//if (matConn2.shaderFilePath == null || deserializedObject.shaderFilePath != "NULL")
															//	matConn2.shaderFilePath = deserializedObject.shaderFilePath;
															//if (matConn2.shaderLocalPath == null || deserializedObject.shaderLocalPath != "NULL")
															//	matConn2.shaderLocalPath = deserializedObject.shaderLocalPath;
															//matConn2.ownerId = deserializedObject.matCompId;
															//lock (AssetManager.OwnerIdToMaterial)
															//	AssetManager.OwnerIdToMaterial.Add(deserializedObject.matCompId, matConn2);
															//renderer.gameObject.name = " - MatId: " + deserializedObject.matCompId.ToString();
															//myLogger.PushMessage($"MeshRenderer registered new mat conn: {deserializedObject.matCompId}");

															//var mat = new GameObject("");
															//matConn2.proxy = mat;
															//var rends = mat.AddComponent<MatDebug>();
															//mat.transform.parent = matsRootStatic.transform;
															//mat.name = $"Mat: {deserializedObject.matCompId}, {deserializedObject.shaderFilePath}, {deserializedObject.shaderLocalPath}";
															////mat.name = "NULL";
															//var rend = mat.AddComponent<MeshRenderer>();
															//matConn2.renderers.Add(rend);
															//rends.TheList.Add(rend);
															//rends.matConn = matConn;

															//matConn2.ApplyChanges(new Queue<MaterialAction>()); // to get the mat to instantiate if its null

															// to here

															//Task.Run(async () => 
															//{ 
															//	await Task.Delay(10000);
															//	RunSynchronously(() => 
															//	{
																	
															//	});
																
															//});
															

															//Task.Run(async () => 
															//{ 
																//await Task.Delay(1000);
																//RunSynchronously(() => 
																//{
																	//matConn2.ApplyChanges(new Queue<MaterialAction>());
																//});
															//});

															

															// to here

															//if (AssetManager.FilePathToShader.TryGetValue(deserializedObject.shaderFilePath, out var shadConn))
															//{
															//	myLogger.PushMessage($"created new mat in mesh renderer code 2");
															//	matConn.mat = new Material(shadConn.shader);
															//	matConn.shader = shadConn.shader;
															//	renderer.sharedMaterial = matConn.mat;
															//}
															//else
															//{
															//	//matConn.mat = new Material(DefaultMat.shader);
															//	//renderer.sharedMaterial = matConn.mat;
															//}
														}
													}
													

													//if (deserializedObject.shaderPath != null)
														//matConnMain.shaderPath = deserializedObject.shaderPath;

													MeshConnector meshConn;
													if (AssetManager.OwnerIdToMesh.TryGetValue(deserializedObject.meshCompId, out meshConn))
													{
														filter.mesh = meshConn.mesh;
													}
													else
													{
														if (AssetManager.LocalPathToMesh.TryGetValue(deserializedObject.meshPath, out meshConn))
														{
															filter.mesh = meshConn.mesh;
														}
														else
														{
															filter.mesh = new();
															meshConn = new();
															meshConn.mesh = filter.mesh;
															if (deserializedObject.meshCompId != default)
															{
																AssetManager.OwnerIdToMesh.Add(deserializedObject.meshCompId, meshConn);
															}
															else
															{
																AssetManager.LocalPathToMesh.Add(deserializedObject.meshPath, meshConn);
															}
														}
													}
												}
												else
												{
													SkinnedMeshRenderer skinned = null;
													skinned = go.GetComponent<SkinnedMeshRenderer>();
													if (skinned == null)
													{
														skinned = go.AddComponent<SkinnedMeshRenderer>();
													}
													skinned.sharedMaterial = null;//DefaultMat;
													skinned.enabled = true;

													// from here

													//myLogger.PushMessage($"MeshRenderer needs mat: {deserializedObject.matCompId} {deserializedObject.shaderPath}");

													skinned.gameObject.name = " - MatId: " + deserializedObject.matCompId.ToString();

													//MaterialConnector matConnMain = null;

													if (deserializedObject.matCompId != default)
													{
														if (AssetManager.OwnerIdToMaterial.TryGetValue(deserializedObject.matCompId, out MaterialConnector matConn))
														{
															//matConnMain = matConn;
															if (matConn.mat == null)
															{
																//if (AssetManager.FilePathToShader.TryGetValue(deserializedObject.shaderFilePath, out var shadConn))
																//{
																//	myLogger.PushMessage($"created new mat in mesh renderer code");
																//	matConn.mat = new Material(shadConn.shader);
																//	matConn.shader = shadConn.shader;
																//	renderer.sharedMaterial = matConn.mat;
																//}
																//else
																//{
																//	//matConn.mat = new Material(DefaultMat.shader);
																//	//renderer.sharedMaterial = matConn.mat;
																//}

															}
															else
															{
																//if (matConn.mat.shader != matConn.shader)
																//{
																//matConn.mat.shader = matConn.shader;
																//}
																skinned.sharedMaterial = matConn.mat;
															}

															if (!matConn.skinnedRenderers.Contains(skinned))
															{
																matConn.skinnedRenderers.Add(skinned);
																if (matConn.proxy != null)
																{
																	var proxy = matConn.proxy.GetComponent<MatDebug>();
																	proxy.TheSkinnedList.Add(skinned);
																}
															}

															//myLogger.PushMessage($"MeshRenderer found existing mat: {deserializedObject.matId}");
														}
														else
														{
															// here

															MaterialConnector matConn2 = new();
															//matConnMain = matConn2;
															matConn2.skinnedRenderers.Add(skinned);
															if (matConn2.shaderFilePath == null || deserializedObject.shaderFilePath != "NULL")
																matConn2.shaderFilePath = deserializedObject.shaderFilePath;
															if (matConn2.shaderLocalPath == null || deserializedObject.shaderLocalPath != "NULL")
																matConn2.shaderLocalPath = deserializedObject.shaderLocalPath;
															matConn2.ownerId = deserializedObject.matCompId;
															lock (AssetManager.OwnerIdToMaterial)
																AssetManager.OwnerIdToMaterial.Add(deserializedObject.matCompId, matConn2);
															skinned.gameObject.name = " - MatId: " + deserializedObject.matCompId.ToString();
															myLogger.PushMessage($"MeshRenderer registered new mat conn: {deserializedObject.matCompId}");

															var mat = new GameObject("");
															matConn2.proxy = mat;
															var rends = mat.AddComponent<MatDebug>();
															mat.transform.parent = matsRootStatic.transform;
															mat.name = $"Mat: {deserializedObject.matCompId}, {deserializedObject.shaderFilePath}, {deserializedObject.shaderLocalPath}";
															//mat.name = "NULL";
															var rend = mat.AddComponent<MeshRenderer>();
															matConn2.renderers.Add(rend);
															rends.TheList.Add(rend);
															rends.matConn = matConn;

															matConn2.ApplyChanges(new Queue<MaterialAction>()); // to get the mat to instantiate if its null

															//Task.Run(async () => 
															//{ 
															//	await Task.Delay(10000);
															//	RunSynchronously(() => 
															//	{

															//	});

															//});


															//Task.Run(async () => 
															//{ 
															//await Task.Delay(1000);
															//RunSynchronously(() => 
															//{
															//matConn2.ApplyChanges(new Queue<MaterialAction>());
															//});
															//});



															// to here

															//if (AssetManager.FilePathToShader.TryGetValue(deserializedObject.shaderFilePath, out var shadConn))
															//{
															//	myLogger.PushMessage($"created new mat in mesh renderer code 2");
															//	matConn.mat = new Material(shadConn.shader);
															//	matConn.shader = shadConn.shader;
															//	renderer.sharedMaterial = matConn.mat;
															//}
															//else
															//{
															//	//matConn.mat = new Material(DefaultMat.shader);
															//	//renderer.sharedMaterial = matConn.mat;
															//}
														}
													}

													// to here


													//if (AssetManager.OwnerIdToMaterial.TryGetValue(deserializedObject.matId, out MaterialConnector matConn))
													//{
													//skinned.material = matConn.mat;
													//}
													if (deserializedObject.meshCompId != default)
													{
														skinned.gameObject.name += " - MatId: " + deserializedObject.matCompId.ToString();
														//myLogger.PushMessage($"MeshRenderer needs mat: {deserializedObject.matId}");
														if (AssetManager.OwnerIdToMaterial.TryGetValue(deserializedObject.matCompId, out MaterialConnector matConn))
														{
															skinned.sharedMaterial = matConn.mat;
															//myLogger.PushMessage($"SkinnedMeshRenderer found existing mat: {deserializedObject.matId}");
														}
														else
														{
															MaterialConnector matConn2 = new();
															matConn2.shaderFilePath = deserializedObject.shaderFilePath;
															lock (AssetManager.OwnerIdToMaterial)
																AssetManager.OwnerIdToMaterial.Add(deserializedObject.matCompId, matConn2);
															myLogger.PushMessage($"SkinnedMeshRenderer registered new mat using default shader");
														}
													}
													else
													{
														//myLogger.PushMessage($"SkinnedMeshRenderer ownerId is null. using default mat.");
													}
													Mesh mesh = null;
													if (AssetManager.OwnerIdToMesh.TryGetValue(deserializedObject.meshCompId, out MeshConnector meshConn))
													{
														//skinned.sharedMesh = meshConn.mesh;
														mesh = meshConn.mesh;
													}
													else
													{
														if (AssetManager.LocalPathToMesh.TryGetValue(deserializedObject.meshPath, out meshConn))
														{
															//skinned.sharedMesh = meshConn.mesh;
															mesh = meshConn.mesh;
														}
														else
														{
															//skinned.sharedMesh = new();
															mesh = new();
															meshConn = new();
															meshConn.mesh = mesh;
															if (deserializedObject.meshCompId != default)
															{
																AssetManager.OwnerIdToMesh.Add(deserializedObject.meshCompId, meshConn);
															}
															else
															{
																AssetManager.LocalPathToMesh.Add(deserializedObject.meshPath, meshConn);
															}
														}
													}

													skinned.sharedMesh = mesh;

													int boneCount = deserializedObject.boneRefIds.Count;
													int blendShapeCount = deserializedObject.blendShapeWeights.Count;
													bool isBlendshapeOnly = boneCount == 0 && blendShapeCount > 0;

													Transform GetRootBone()
													{
														Transform bone = null;
														foreach (var b in deserializedObject.boneRefIds)
														{
															if (world.refIdToSlot.TryGetValue(b, out var boneSlot) && (bone == null || bone.IsChildOf(boneSlot.ForceGetGameObject().transform)))
															{
																bone = boneSlot.ForceGetGameObject().transform;
															}
														}
														return bone;
													}

													// do bones
													Transform[] newBonesArr;
													if (isBlendshapeOnly)
													{
														newBonesArr = new Transform[1];
														newBonesArr[0] = skinned.gameObject.transform;
													}
													else
													{
														newBonesArr = new Transform[deserializedObject.boneRefIds.Count];
														int i = 0;
														foreach (var refId in deserializedObject.boneRefIds)
														{
															newBonesArr[i] = skinned.gameObject.transform;

															if (refId != default && world.refIdToSlot.TryGetValue(refId, out var boneSlot))
															{
																if (boneSlot.ForceGetGameObject() != null && boneSlot.ForceGetGameObject().transform != null)
																{
																	newBonesArr[i] = boneSlot.ForceGetGameObject().transform;
																}
																else
																{
																	myLogger.PushMessage("Bone slot has null GameObject or transform, using fallback");
																}
															}
															else
															{
																myLogger.PushMessage(refId == default ?
																	"Default refId, using fallback bone transform" :
																	"Failed to get bone transform for skinned renderer");
															}

															///

															//if (refId == default)
															//{
															//	skinned.bones[i] = skinned.gameObject.transform;
															//	myLogger.PushMessage("Using fallback bone transform");
															//}
															//else
															//{
															//	if (world.refIdToSlot.TryGetValue(refId, out var boneSlot))
															//	{
															//		skinned.bones[i] = boneSlot.GeneratedGameObject.transform;
															//	}
															//	else
															//	{
															//		skinned.bones[i] = skinned.gameObject.transform;
															//		myLogger.PushMessage("Failed to get bone transform for skinned renderer");
															//	}
															//}

															if (newBonesArr[i] == null)
															{
																myLogger.PushMessage("CRITICAL: Bone still null after assignment, using fallback");
																newBonesArr[i] = skinned.gameObject.transform;
															}

															i++;
														}
													}

													skinned.bones = newBonesArr;

													//skinned.updateWhenOffscreen = true; // needed?

													skinned.rootBone = isBlendshapeOnly ? skinned.gameObject.transform : GetRootBone();

													//myLogger.PushMessage($"GetRootBoneNull? {GetRootBone() == null}");

													// Validate the entire bones array as a final check
													for (int j = 0; j < skinned.bones.Length; j++)
													{
														if (skinned.bones[j] == null)
														{
															myLogger.PushMessage($"CRITICAL: Bone at index {j} is null after setup complete");
															skinned.bones[j] = skinned.rootBone;
														}
													}

													for (int i2 = 0; i2 < deserializedObject.blendShapeWeights.Count; i2++)
													{
														skinned.SetBlendShapeWeight(i2, deserializedObject.blendShapeWeights[i2]);
													}

													//skinned.forceMatrixRecalculationPerRender = true;
												
													if (skinned.sharedMesh.bounds.size.magnitude < 0.01f)
													{
														myLogger.PushMessage("Mesh bounds are very small, may be culled.");
														skinned.sharedMesh.RecalculateBounds();
													}

													//myLogger.PushMessage($"Mesh vertices: {skinned.sharedMesh.vertexCount}, Has bones: {skinned.bones.Length > 0} Valid bones: {skinned.bones.All(b => b != null)} rootBoneNull? {skinned.rootBone == null}");
													//myLogger.PushMessage($"bone count {skinned.bones.Length} bindpose count {skinned.sharedMesh.bindposeCount} weight count {skinned.sharedMesh.boneWeights.Length}");

													bool validHierarchy = true;
													foreach (Transform bone in skinned.bones)
													{
														if (bone is null)
														{
															myLogger.PushMessage("A bone is null!");
															validHierarchy = false;
															break;
														}
														//if (!bone.IsChildOf(skinned.rootBone) && bone != skinned.rootBone)
														//{
															//myLogger.PushMessage("A bone is not a child of the root bone!");
															//validHierarchy = false;
															//break;
														//}
													}
													if (!validHierarchy)
													{
														myLogger.PushMessage("Invalid bones hierarchy!");
													}
													//else
													//{
														//myLogger.PushMessage("Bone hierarchy is valid!");
													//}

													//skinned.ResetBounds();
												}
											}
										}
									});
								}
								//else if (num == (int)PacketTypes.LoadFromFileShader)
								//{
								//	LoadFromFileShaderConnector deserializedObject = new();
								//	deserializedObject.Deserialize(buffer);

								//	RunSynchronously(() => myLogger.PushMessage($"LoadFromFileShader: {deserializedObject.File}"));

								//	RunSynchronously(() => ShaderConnector.LoadFromFileShader2(deserializedObject));
								//}
								else if (num == (int)PacketTypes.ApplyChangesMesh)
								{
									ApplyChangesMeshConnector deserializedObject = new();
									deserializedObject.Deserialize(buffer);

									MeshConnector meshConn;

									RunSynchronously(() => 
									{
										if (!AssetManager.OwnerIdToMesh.TryGetValue(deserializedObject.ownerId, out meshConn))
										{
											if (!AssetManager.LocalPathToMesh.TryGetValue(deserializedObject.localPath, out meshConn))
											{
												meshConn = new();
												meshConn.mesh = new();
												if (deserializedObject.ownerId != default)
												{
													AssetManager.OwnerIdToMesh.Add(deserializedObject.ownerId, meshConn);
												}
												else
												{
													AssetManager.LocalPathToMesh.Add(deserializedObject.localPath, meshConn);
												}
											}
										}
										meshConn.ApplyChanges(deserializedObject);
									});
								}
								else if (num == (int)PacketTypes.ApplyChangesMaterial)
								{
									ApplyChangesMaterialConnector deserializedObject = new();
									deserializedObject.Deserialize(buffer);

									//RunSynchronously(() => myLoggerStatic.PushMessage(deserializedObject.ToString()));

									RunSynchronously(() => 
									{
										MaterialConnector matConn = null;
										if (deserializedObject.ownerId != default)
										{
											if (!AssetManager.OwnerIdToMaterial.TryGetValue(deserializedObject.ownerId, out matConn))
											{
												matConn = new();
												//matConn.mat = new Material(DefaultMat.shader);

												lock (AssetManager.OwnerIdToMaterial)
													AssetManager.OwnerIdToMaterial.Add(deserializedObject.ownerId, matConn);

												myLoggerStatic.PushMessage($"ApplyChangesMaterial registered new matConn: {deserializedObject.ownerId}");

												var mat = new GameObject("");
												matConn.proxy = mat;
												var rends = mat.AddComponent<MatDebug>();
												mat.transform.parent = matsRootStatic.transform;
												mat.name = $"Mat: {deserializedObject.ownerId}, {deserializedObject.shaderFilePath}, {deserializedObject.shaderLocalPath}";
												var rend = mat.AddComponent<MeshRenderer>();
												matConn.renderers.Add(rend);
												rends.TheList.Add(rend);
												rends.matConn = matConn;
											}

											matConn.ownerId = deserializedObject.ownerId;
											if (matConn.shaderFilePath != "NULL")
												matConn.shaderFilePath = deserializedObject.shaderFilePath;
											if (matConn.shaderLocalPath != "NULL")
												matConn.shaderLocalPath = deserializedObject.shaderLocalPath;

											if (matConn.proxy != null)
												matConn.proxy.name = $"Mat: {deserializedObject.ownerId}, {deserializedObject.shaderFilePath}, {deserializedObject.shaderLocalPath}";

											matConn.ApplyChanges(deserializedObject.actionQueue);
										}
										
									});
								}
								else if (num == (int)PacketTypes.InitializeMaterialProperties)
								{
									InitializeMaterialPropertiesPacket deserializedObject = new();
									deserializedObject.Deserialize(buffer);

									RunSynchronously(() => myLoggerStatic.PushMessage("InitializeMaterialProperties received"));	

									InitializeMaterialPropertiesPacket returnPacket = new();
									returnPacket.PropertyIds = new();
									foreach (var name in deserializedObject.PropertyNames)
									{
										returnPacket.PropertyIds.Add(Shader.PropertyToID(name));
									}
									Main.QueuePacket(returnPacket);
								}
							}
						}
						catch (Exception e)
						{
							myLogger.PushMessage($"Error in main buffer loop: {e}");
							throw;
						}
						//updates++;
						//if (updates > 25)
						//{
						//	updates = 0;
						//	await Task.Delay(TimeSpan.FromMilliseconds(1));
						//}
					}
				});
			}
			if (started && buffer != null && buffer.ShuttingDown && !DEBUG)
			{
				Process.GetCurrentProcess().Kill();
			}
			if (UnityEngine.Input.GetMouseButton(0))
			{
				float deltaX = Input.GetAxis("Mouse X") * camSpeed;
				float deltaY = Input.GetAxis("Mouse Y") * camSpeed;
				camera1.transform.RotateAround(camera1.transform.position, Vector3.up, deltaX);

				// Calculate new vertical angle and clamp it
				currentVerticalAngle -= deltaY; // Subtract because negative deltaY = looking up
				currentVerticalAngle = Mathf.Clamp(currentVerticalAngle, -verticalAngleLimit, verticalAngleLimit);

				// Reset camera rotation first (to avoid accumulation issues)
				Vector3 eulerAngles = camera1.transform.eulerAngles;
				camera1.transform.rotation = Quaternion.Euler(currentVerticalAngle, eulerAngles.y, 0f);
			}
			if (UnityEngine.Input.GetKey(KeyCode.C))
			{
				camera1.transform.Translate(Vector3.down * moveSpeed * Time.deltaTime);
			}
			if (UnityEngine.Input.GetKey(KeyCode.Space))
			{
				camera1.transform.Translate(Vector3.up * moveSpeed * Time.deltaTime);
			}
			if (UnityEngine.Input.GetKey(KeyCode.A))
			{
				camera1.transform.Translate(Vector3.left * moveSpeed * Time.deltaTime);
			}
			if (UnityEngine.Input.GetKey(KeyCode.D))
			{
				camera1.transform.Translate(Vector3.right * moveSpeed * Time.deltaTime);
			}
			if (UnityEngine.Input.GetKey(KeyCode.S))
			{
				camera1.transform.Translate(Vector3.back * moveSpeed * Time.deltaTime);
			}
			if (UnityEngine.Input.GetKey(KeyCode.W))
			{
				camera1.transform.Translate(Vector3.forward * moveSpeed * Time.deltaTime);
			}
			if (synchronousActions.Count > 0)
			{
				Queue<Action> actions = new();
				//int count = Math.Min(synchronousActions.Count, (int)(1f / Time.deltaTime) * 30);
				int count = synchronousActions.Count;
				lock (synchronousActions)
				{
					actions = new Queue<Action>(count);
					for (int i = 0; i < count; i++)
					{
						actions.Enqueue(synchronousActions.Dequeue());
					}
				}
				while (actions.Count > 0)
				{
					Action act = actions.Dequeue();
					try
					{
						act();
					}
					catch (Exception ex)
					{
						myLogger.PushMessage("Error in synchronous action: " + ex.ToString());
					}
				}
			}
			
			//int n = 5;
			//returnBuffer.Write(ref n);
		}

		// Supposedly this is better for input handling because it runs at a constant time interval, but last I tried it was icky...
		private void FixedUpdate()
		{
		}

		public static void RunSynchronously(Action act)
		{
			lock (synchronousActions)
				synchronousActions.Enqueue(act);
		}
	}
}