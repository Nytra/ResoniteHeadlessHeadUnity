using UnityEngine;
using System.IO;
using System;
using System.Threading.Tasks;
using System.Diagnostics;
using System.IO.Pipes;
using System.Collections.Generic;
using SharedMemory;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Animations;
using Unity.VisualScripting;

namespace Thundagun
{
	public class WorldManager
	{
		public static Dictionary<long, WorldConnector> idToWorld = new();
		public static Dictionary<GameObject, WorldConnector> goToWorld = new();
	}

	public class AssetManager
	{
		public static Dictionary<string, ShaderConnector> LocalPathToShader = new();
	}

	public enum PacketTypes
	{
		Sync,
		ApplyChangesSlot,
		DestroySlot,
		InitializeWorld,
		ChangeFocusWorld,
		DestroyWorld,
		ApplyChangesMeshRenderer,
		DestroyMeshRenderer,
		LoadFromFileShader
	}

	public class Main : MonoBehaviour
	{
		public MyLogger myLogger;
		public GameObject worldsRoot;
		public GameObject camera1;
		public Material DefaultMat;
		public float moveSpeed;
		public float camSpeed;
		private static bool started = false;
		private static CircularBuffer buffer;
		private static Queue<Action> synchronousActions = new();
		//private static int updates = 0;
		const bool DEBUG = true; // use when running in the Unity editor, so that the editor doesn't get closed

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

				var args = Environment.GetCommandLineArgs();
				if (args != null)
				{
					myLogger.PushMessage(string.Join(',', args));
				}

				var syncBuffer = new BufferReadWrite("SyncBuffer");
				var returnBuffer = new CircularBuffer("ReturnBuffer");

				myLogger.PushMessage("SyncBuffer opened.");

				int num;

				myLogger.PushMessage("Waiting for SyncBuffer message...");

				syncBuffer.Read(out num);

				myLogger.PushMessage($"Got id {num} from SyncBuffer.");

				buffer = new CircularBuffer($"MyBuffer{num}");
				
				myLogger.PushMessage($"MyBuffer{num} opened.");

				myLogger.PushMessage($"Returning id {num}.");

				returnBuffer.Write(ref num);

				myLogger.PushMessage("Starting message loop!!!");

				num = 0;

				Task.Run(async () =>
				{
					//while (buffer.NodeCount > 0)
					//{
					//	var bytes = new byte[128];
					//	buffer.Read(bytes);
					//}
					while (true)
					{
						buffer.Read(out num);

						if (num != 0)
						{
							//RunSynchronously(() =>
							//{ 
							//	myLogger.PushMessage(num.ToString());
							//});
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

											var text = slotConn.GeneratedGameObject.GetComponentInChildren<TextMeshPro>();
											if ((deserializedObject.ShouldRender || slotConn.ForceRender || deserializedObject.IsUserRootSlot || deserializedObject.IsRootSlot) && deserializedObject.Active)
											{
												//text.gameObject.transform.parent.gameObject.GetComponent<LookAtConstraint>().enabled = true;
												text.text = deserializedObject.SlotName;
											}
											else
											{
												//text.gameObject.transform.parent.gameObject.GetComponent<LookAtConstraint>().enabled = false;
												text.text = "";
											}

											if (deserializedObject.HasActiveUser)
											{
												//slotConn.GeneratedGameObject.transform.GetChild(0).GetComponentInChildren<MeshRenderer>().material.color = Color.green;
												slotConn.GeneratedGameObject.GetComponentInChildren<TextMeshPro>().color = Color.green;
											}
											else
											{
												//slotConn.GeneratedGameObject.transform.GetChild(0).GetComponentInChildren<MeshRenderer>().material.color = Color.white;
												slotConn.GeneratedGameObject.GetComponentInChildren<TextMeshPro>().color = Color.white;
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

											newSc.parentId = deserializedObject.ParentRefId;

											if (deserializedObject.ForceRender)
												newSc.ForceRender = true;

											var go = newSc.RequestGameObject();
											go.name = deserializedObject.SlotName;

											var text = go.GetComponentInChildren<TextMeshPro>();
											if ((deserializedObject.ShouldRender || newSc.ForceRender || deserializedObject.IsUserRootSlot || deserializedObject.IsRootSlot) && deserializedObject.Active)
											{
												//text.gameObject.transform.parent.gameObject.GetComponent<LookAtConstraint>().enabled = true;
												text.text = deserializedObject.SlotName;
											}
											else
											{
												//text.gameObject.transform.parent.gameObject.GetComponent<LookAtConstraint>().enabled = false;
												text.text = "";
											}

											if (deserializedObject.HasActiveUser)
											{
												//go.transform.GetChild(0).GetComponentInChildren<MeshRenderer>().material.color = Color.green;
												go.GetComponentInChildren<TextMeshPro>().color = Color.green;
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

								if (WorldManager.idToWorld.TryGetValue(deserializedObject.WorldId, out var world))
								{
									if (world.WorldRoot) UnityEngine.Object.Destroy(world.WorldRoot);
									world.WorldRoot = null;
								}
							}
							else if (num == (int)PacketTypes.ApplyChangesMeshRenderer)
							{
								ApplyChangesMeshRendererConnector deserializedObject = new();
								deserializedObject.Deserialize(buffer);

								RunSynchronously(() => 
								{
									myLogger.PushMessage(deserializedObject.ToString());

									if (WorldManager.idToWorld.TryGetValue(deserializedObject.worldId, out var world))
									{
										if (world.refIdToSlot.TryGetValue(deserializedObject.slotRefId, out var slot))
										{
											var go = slot.RequestGameObject();

											var meshRendererConnector = new MeshRendererConnector();
											slot.Meshes.Add(meshRendererConnector);
											meshRendererConnector.mesh = new Mesh();

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
												renderer.material = DefaultMat;
												renderer.enabled = true;
												if (AssetManager.LocalPathToShader.TryGetValue(deserializedObject.shaderPath, out ShaderConnector shadConn))
												{
													renderer.material = new Material(shadConn.shader);
												}
												filter.mesh = meshRendererConnector.mesh;
												//filter.mesh.MarkDynamic();
												filter.mesh.Clear();

												filter.mesh.SetVertices(deserializedObject.verts);
												filter.mesh.SetNormals(deserializedObject.normals);
												filter.mesh.SetTangents(deserializedObject.tangents);
												filter.mesh.SetTriangles(deserializedObject.triangleIndices, 0);
												filter.mesh.SetColors(deserializedObject.colors);

												filter.mesh.RecalculateBounds();
												//filter.mesh.UploadMeshData(false);
											}
											else
											{
												SkinnedMeshRenderer skinned = null;
												skinned = go.GetComponent<SkinnedMeshRenderer>();
												if (skinned == null)
												{
													skinned = go.AddComponent<SkinnedMeshRenderer>();
												}
												skinned.material = DefaultMat;
												skinned.enabled = true;
												if (AssetManager.LocalPathToShader.TryGetValue(deserializedObject.shaderPath, out ShaderConnector shadConn))
												{
													skinned.material = new Material(shadConn.shader);
												}
												skinned.sharedMesh = meshRendererConnector.mesh;
												//skinned.sharedMesh.MarkDynamic();
												skinned.sharedMesh.Clear();

												skinned.sharedMesh.SetVertices(deserializedObject.verts);
												skinned.sharedMesh.SetNormals(deserializedObject.normals);
												skinned.sharedMesh.SetTangents(deserializedObject.tangents);
												skinned.sharedMesh.SetTriangles(deserializedObject.triangleIndices, 0);
												skinned.sharedMesh.SetColors(deserializedObject.colors);
												skinned.sharedMesh.boneWeights = deserializedObject.boneWeights.ToArray();
												skinned.sharedMesh.bindposes = deserializedObject.bindPoses.ToArray();

												// do transforms
												skinned.bones = new Transform[deserializedObject.boneRefIds.Count];
												int i = 0;
												foreach (var refId in deserializedObject.boneRefIds)
												{
													if (world.refIdToSlot.TryGetValue(refId, out var boneSlot))
													{
														skinned.bones[i] = boneSlot.RequestGameObject().transform;
													}
													else
													{
														myLogger.PushMessage("Failed to get bone transform for skinned renderer");
													}
													i++;
												}

												skinned.sharedMesh.RecalculateBounds();
												//skinned.sharedMesh.UploadMeshData(false);

												//skinned.
											}
										}
									}
								});
							}
							else if (num == (int)PacketTypes.LoadFromFileShader)
							{
								LoadFromFileShaderConnector deserializedObject = new();
								deserializedObject.Deserialize(buffer);

								RunSynchronously(() => 
								{
									try
									{
										var bundleRequest = AssetBundle.LoadFromFileAsync(deserializedObject.File);
										AssetBundleRequest shaderRequest;
										bundleRequest.completed += delegate
										{
											try
											{
												if (bundleRequest.assetBundle == null)
												{
													myLogger.PushMessage($"Could not load shader asset bundle: {deserializedObject.File}, exists: {File.Exists(deserializedObject.File)}");
												}
												else
												{
													shaderRequest = bundleRequest.assetBundle.LoadAssetAsync<Shader>(bundleRequest.assetBundle.GetAllAssetNames()[0]);
													shaderRequest.completed += delegate
													{
														try
														{
															ShaderConnector shad = new();
															shad.shader = shaderRequest.asset as Shader;
															if (!AssetManager.LocalPathToShader.ContainsKey(deserializedObject.LocalPath))
															{
																AssetManager.LocalPathToShader.Add(deserializedObject.LocalPath, shad);
																myLogger.PushMessage($"Successfully loaded a shader from the bundle {deserializedObject.File}");
															}
														}
														catch (Exception arg2)
														{
															myLogger.PushMessage($"Exception loading shader from the loaded bundle {deserializedObject.File}\n{arg2}");
														}
													};
												}
											}
											catch (Exception arg)
											{
												myLogger.PushMessage($"Exception processing loaded shader bundle for {deserializedObject.File}\n{arg}");
											}
										};
									}
									catch (Exception ex)
									{
										myLogger.PushMessage("Exception loading shader from file: " + deserializedObject.File + "\n" + ex);
									}
								});
							}
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
				camera1.transform.RotateAround(camera1.transform.position, Vector3.up, deltaX);
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
						myLogger.PushMessage(ex.ToString());
					}
				}
			}
		}

		// Supposedly this is better for input handling because it runs at a constant time interval, but last I tried it was icky...
		private void FixedUpdate()
		{
		}

		private void RunSynchronously(Action act)
		{
			lock (synchronousActions)
				synchronousActions.Enqueue(act);
		}
	}
}