using UnityEngine;
using System.IO;
using System;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Collections.Generic;
using SharedMemory;
using TMPro;
using UnityEngine.Rendering;
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
		public static Dictionary<string, ShaderConnector> LocalPathToShader = new();
		public static Dictionary<ulong, MeshConnector> OwnerIdToMesh = new();
		public static Dictionary<string, MeshConnector> LocalPathToMesh = new();
	}

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
		ApplyChangesMesh
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
		private static CircularBuffer returnBuffer;
		private static BufferReadWrite syncBuffer;
		private static Queue<Action> synchronousActions = new();

		// Track the current vertical angle
		private float currentVerticalAngle = 0f;
		private float verticalAngleLimit = 80f; // Limit in degrees, adjust as needed

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

				Task.Run(async () =>
				{
					while (true)
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
												renderer.material = DefaultMat;
												renderer.enabled = true;
												if (AssetManager.LocalPathToShader.TryGetValue(deserializedObject.shaderPath, out ShaderConnector shadConn))
												{
													renderer.material = new Material(shadConn.shader);
												}
												MeshConnector meshConn;
												if (AssetManager.OwnerIdToMesh.TryGetValue(deserializedObject.ownerId, out meshConn))
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
														if (deserializedObject.ownerId != default)
														{
															AssetManager.OwnerIdToMesh.Add(deserializedObject.ownerId, meshConn);
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
												skinned.material = DefaultMat;
												skinned.enabled = true;
												if (AssetManager.LocalPathToShader.TryGetValue(deserializedObject.shaderPath, out ShaderConnector shadConn))
												{
													skinned.material = new Material(shadConn.shader);
												}
												Mesh mesh = null;
												if (AssetManager.OwnerIdToMesh.TryGetValue(deserializedObject.ownerId, out MeshConnector meshConn))
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
														if (deserializedObject.ownerId != default)
														{
															AssetManager.OwnerIdToMesh.Add(deserializedObject.ownerId, meshConn);
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

												myLogger.PushMessage($"Mesh vertices: {skinned.sharedMesh.vertexCount}, Has bones: {skinned.bones.Length > 0} Valid bones: {skinned.bones.All(b => b != null)} rootBoneNull? {skinned.rootBone == null}");
												myLogger.PushMessage($"bone count {skinned.bones.Length} bindpose count {skinned.sharedMesh.bindposeCount} weight count {skinned.sharedMesh.boneWeights.Length}");

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
							else if (num == (int)PacketTypes.LoadFromFileShader)
							{
								LoadFromFileShaderConnector deserializedObject = new();
								deserializedObject.Deserialize(buffer);

								RunSynchronously(() => 
								{
									myLogger.PushMessage(deserializedObject.ToString());
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
															if (!AssetManager.LocalPathToShader.ContainsKey(deserializedObject.LocalPath))
															{
																ShaderConnector shad = new();
																shad.shader = shaderRequest.asset as Shader;
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
							else if (num == (int)PacketTypes.ApplyChangesMesh)
							{
								ApplyChangesMeshConnector deserializedObject = new();
								deserializedObject.Deserialize(buffer);

								RunSynchronously(() => 
								{
									//if (deserializedObject.localPath.Trim() == "NULL") return;

									MeshConnector meshConn;
									
									Mesh mesh;
									if (AssetManager.OwnerIdToMesh.TryGetValue(deserializedObject.ownerId, out meshConn))
									{
										mesh = meshConn.mesh;
									}
									else
									{
										if (AssetManager.LocalPathToMesh.TryGetValue(deserializedObject.localPath, out meshConn))
										{
											mesh = meshConn.mesh;
										}
										else
										{
											mesh = new();
											meshConn = new();
											meshConn.mesh = mesh;
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

									mesh.Clear();
									if (deserializedObject.verts.Length > 0)
										mesh.SetVertices(deserializedObject.verts);
									mesh.indexFormat = ((((deserializedObject.verts.Length > 0) ? deserializedObject.verts.Length : 0) > 65535) ? IndexFormat.UInt32 : IndexFormat.UInt16);
									//mesh.MarkDynamic(); // is this needed?
									if (deserializedObject.normals.Length > 0)
										mesh.SetNormals(deserializedObject.normals);
									if (deserializedObject.tangents.Length > 0)
										mesh.SetTangents(deserializedObject.tangents);
									if (deserializedObject.triangleIndices.Length > 0)
										mesh.SetTriangles(deserializedObject.triangleIndices, 0);
									if (deserializedObject.colors.Length > 0)
										mesh.SetColors(deserializedObject.colors);

									bool isBlendshapeOnly = deserializedObject.blendShapeFrames.Length > 0 && deserializedObject.boneWeights.Length == 0;

									Matrix4x4[] newBindPoseArr = null;
									BoneWeight[] newBoneWeightArr = null;
									if (isBlendshapeOnly)
									{
										//mesh.bindposes = new Matrix4x4[1];
										newBindPoseArr = new Matrix4x4[1];
										newBoneWeightArr = new BoneWeight[deserializedObject.verts.Length];
									}
									else
									{
										//mesh.boneWeights = new BoneWeight[deserializedObject.bindPoses.Length > 0 ? deserializedObject.verts.Length : 0];
										newBoneWeightArr = new BoneWeight[deserializedObject.bindPoses.Length > 0 ? deserializedObject.verts.Length : 0]; // needed?
										newBindPoseArr = new Matrix4x4[deserializedObject.boneWeights.Length]; // or Bone Count length?
									}

									if (isBlendshapeOnly)
									{
										newBindPoseArr[0] = Matrix4x4.identity;
										BoneWeight boneWeight = default(BoneWeight);
										boneWeight.boneIndex0 = 0;
										boneWeight.boneIndex1 = 0;
										boneWeight.boneIndex2 = 0;
										boneWeight.boneIndex3 = 0;
										boneWeight.weight0 = 1f;
										boneWeight.weight1 = 0f;
										boneWeight.weight2 = 0f;
										boneWeight.weight3 = 0f;
										for (int l = 0; l < deserializedObject.verts.Length; l++)
										{
											newBoneWeightArr[l] = boneWeight;
										}
										mesh.boneWeights = newBoneWeightArr;
										mesh.bindposes = newBindPoseArr;
									}
									else
									{
										if (deserializedObject.bindPoses.Length > 0)
										{
											mesh.bindposes = deserializedObject.bindPoses;
											mesh.boneWeights = deserializedObject.boneWeights;
										}
									}
									
									foreach (var blendShapeFrame in deserializedObject.blendShapeFrames)
									{
										mesh.AddBlendShapeFrame(blendShapeFrame.name, blendShapeFrame.weight, blendShapeFrame.positions, blendShapeFrame.normals, blendShapeFrame.tangents);
									}

									//if (deserializedObject.verts.Length > 0) // needed?
										//mesh.SetVertices(deserializedObject.verts);

									mesh.bounds = deserializedObject.bounds;

									//if (deserializedObject.verts.Length > 0)
										//mesh.RecalculateBounds(); // needed?

									mesh.UploadMeshData(false);
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
						myLogger.PushMessage(ex.ToString());
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

		private void RunSynchronously(Action act)
		{
			lock (synchronousActions)
				synchronousActions.Enqueue(act);
		}
	}
}