using UnityEngine;
using System.IO;
using System;
using System.Threading.Tasks;
using System.Diagnostics;
using System.IO.Pipes;
using System.Collections.Generic;
using SharedMemory;

namespace Thundagun
{
	public class WorldManager
	{
		public static Dictionary<long, WorldConnector> idToWorld = new();
		public static Dictionary<GameObject, WorldConnector> goToWorld = new();
	}

	public class Main : MonoBehaviour
	{
		public MyLogger myLogger;
		public GameObject worldsRoot;
		public GameObject camera1;
		public float moveSpeed;
		public float camSpeed;
		private static bool started = false;
		private static NamedPipeClientStream pipeClient = null;
		private static BinaryReader br;
		private static Queue<Action> synchronousActions = new();
		private static int updates = 0;
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

				pipeClient = new NamedPipeClientStream(".", "ResoniteHeadlessHead", PipeDirection.In);

				pipeClient.Connect();

				myLogger.PushMessage("[CLIENT] Current TransmissionMode: " + pipeClient.TransmissionMode.ToString());

				br = new BinaryReader(pipeClient);

				// Display the read text to the console
				string temp;

				// Wait for 'sync message' from the server.
				do
				{
					myLogger.PushMessage("[CLIENT] Wait for sync...");
					temp = br.ReadString();
				}
				while (!temp.StartsWith("SYNC"));

				var thing = new SharedMemory.SharedArray<int>("TestArray");
				myLogger.PushMessage(thing[0].ToString());
				myLogger.PushMessage(thing[1].ToString());
				myLogger.PushMessage(thing[2].ToString());

				myLogger.PushMessage("[CLIENT] synced.");

				Task.Run(async () =>
				{
					while (true)
					{
						var temp = br.ReadString();
						if (temp != null)
						{
							RunSynchronously(() => 
							{ 
								myLogger.PushMessage(temp);
							});
							if (temp == "ApplyChangesSlotConnector")
							{
								ApplyChangesSlotConnector deserializedObject = new();
								deserializedObject.Deserialize(br);

								RunSynchronously(() =>
								{
									myLogger.PushMessage(deserializedObject.ToString());

									if (WorldManager.idToWorld.TryGetValue(deserializedObject.WorldId, out var world))
									{
										if (world.refIdToSlot.TryGetValue(deserializedObject.RefId, out var slotConn))
										{
											slotConn.IsRootSlot = deserializedObject.IsRootSlot;
											slotConn.Active = deserializedObject.Active;
											slotConn.Position = deserializedObject.Position;
											slotConn.Rotation = Quaternion.Euler(deserializedObject.Rotation);
											slotConn.Scale = deserializedObject.Scale;
											slotConn.RefId = deserializedObject.RefId;

											var generatedGameObject = slotConn.GeneratedGameObject;
											if (generatedGameObject == null)
												return;

											if (deserializedObject.Reparent)
											{
												GameObject gameObject;

												if (world.refIdToSlot.TryGetValue(deserializedObject.ParentRefId, out var parentSlot))
												{
													slotConn.ParentConnector = parentSlot;
													gameObject = slotConn.ParentConnector.RequestGameObject();
												}
												else
												{
													if (WorldManager.idToWorld.TryGetValue(deserializedObject.WorldId, out var world2))
													{
														gameObject = world2.WorldRoot;
														myLogger.PushMessage("parenting to world root");
													}
													else
													{
														gameObject = worldsRoot;
														myLogger.PushMessage("WARNING: parenting to worlds root (NOT world root)!");
													}
												}

												slotConn.Transform.SetParent(gameObject.transform, false);
											}

											//slotConn.UpdateLayer();
											slotConn.SetData();

											//UpdateData
											//GameObject go = slotConn.GeneratedGameObject;
											//if (deserializedObject.ActiveChanged) go.SetActive(deserializedObject.Active);
											//if (deserializedObject.PositionChanged) go.transform.position = deserializedObject.Position;
											//if (deserializedObject.RotationChanged) go.transform.rotation = Quaternion.Euler(deserializedObject.Rotation);
											//if (deserializedObject.ScaleChanged) go.transform.localScale = deserializedObject.Scale;
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
											newSc.Rotation = Quaternion.Euler(deserializedObject.Rotation);
											newSc.Scale = deserializedObject.Scale;
											newSc.RefId = deserializedObject.RefId;

											var go = newSc.RequestGameObject();
											go.name = deserializedObject.SlotName;

											world2.refIdToSlot.Add(deserializedObject.RefId, newSc);
											world2.goToSlot.Add(go, newSc);
										}
									}
									else
									{
										myLogger.PushMessage("No world in ApplyChangesSlotConnector!");
									}

									//GameObject go;
									//go = RequestGameObject(deserializedObject);
									//UpdateParent(go, deserializedObject);
								});
							}
							else if (temp == "InitializeWorldConnector")
							{
								InitializeWorldConnector deserializedObject = new();
								deserializedObject.Deserialize(br);

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
							else if (temp == "DestroySlotConnector")
							{
								DestroySlotConnector deserializedObject = new();
								deserializedObject.Deserialize(br);

								RunSynchronously(() =>
								{
									myLogger.PushMessage(deserializedObject.ToString());
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
										myLogger.PushMessage("No world in DestroySlotConnector!");
									}
								});
							}
							else if (temp == "ChangeFocusWorldConnector")
							{
								ChangeFocusWorldConnector deserializedObject = new();
								deserializedObject.Deserialize(br);

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
							else if (temp == "DestroyWorldConnector")
							{
								DestroyWorldConnector deserializedObject = new();
								deserializedObject.Deserialize(br);

								if (WorldManager.idToWorld.TryGetValue(deserializedObject.WorldId, out var world))
								{
									if (world.WorldRoot) UnityEngine.Object.Destroy(world.WorldRoot);
									world.WorldRoot = null;
								}
							}
						}
						updates++;
						if (updates > 10)
						{
							updates = 0;
							await Task.Delay(TimeSpan.FromMilliseconds(1));
						}
					}
				});
			}
			if (started && pipeClient != null && !pipeClient.IsConnected && !DEBUG)
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
				lock (synchronousActions)
				{
					actions = new Queue<Action>(synchronousActions);
					synchronousActions.Clear();
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