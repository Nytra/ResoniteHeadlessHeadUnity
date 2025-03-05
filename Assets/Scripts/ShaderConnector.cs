using System;
using System.IO;
using UnityEngine;
using SharedMemory;
using System.Text;
using System.Linq;
using UnityEngine.UIElements;

namespace Thundagun
{
	public class ShaderConnector
	{
		public Shader shader;
		public string filePath;

		public static void LoadFromFileShader2(string FilePath, MaterialConnector matConn, Action callback = null)
		{
			Main.myLoggerStatic.PushMessage($"LoadFromFileShader FilePath: {FilePath}");
			try
			{
				var bundleRequest = AssetBundle.LoadFromFileAsync(FilePath);
				AssetBundleRequest shaderRequest;
				bundleRequest.completed += delegate
				{
					try
					{
						if (bundleRequest.assetBundle == null)
						{
							Main.myLoggerStatic.PushMessage($"Could not load shader asset bundle: {FilePath}, exists: {File.Exists(FilePath)}");
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
									shad.filePath = FilePath;
									matConn.shader = shad.shader;
									//lock (matConn.mat)
									matConn.mat = new Material(shad.shader);

									//shad.localPath = packet.LocalPath;
									//lock (AssetManager.FilePathToShader)
									AssetManager.FilePathToShader.Add(FilePath, shad);
									Main.myLoggerStatic.PushMessage($"Successfully loaded a shader from the bundle {FilePath}");

									//callback?.Invoke();

									//ShaderLoadedCallback callback2 = new();
									//callback2.shaderPath = FilePath;
									//Main.QueuePacket(callback2);

									// keep it outside of a coroutine
									Main.RunSynchronously(() =>
									//{
									{
										//foreach (var shadConn in AssetManager.FilePathToShader.Values)
										//{

										//	foreach (var matConn2 in AssetManager.OwnerIdToMaterial.Values)
										//	{

										//		//myLogger.PushMessage($"ShaderRetroactive: {matConn.shaderPath} {deserializedObject.LocalPath}");
										//		if (matConn2.shaderPath == shadConn.filePath)
										//		{
										//			foreach (var renderer in matConn2.renderers)
										//			{
										//				renderer.sharedMaterial = matConn2.mat;
										//			}

										//		}
										//	}
										//}

										//matConn.mat = matConn.mat;
										//matConn.shader = matConn.shader;
										foreach (var renderer in matConn.renderers)
										{
											//if (renderer.sharedMaterial.shader == shad.shader) continue;
											//Main.myLoggerStatic.PushMessage($"Applying shader retroactively to a renderer with name: {renderer.gameObject.name}");
											renderer.sharedMaterial = matConn.mat;
											renderer.gameObject.name += " HAS MAT";
											//renderer.sharedMaterial = mat;
											//matConn.shader = shad.shader;
											//matConn.mat = renderer.sharedMaterial;
										}
										foreach (var renderer in matConn.skinnedRenderers)
										{
											//if (renderer.sharedMaterial.shader == shad.shader) continue;
											//Main.myLoggerStatic.PushMessage($"Applying shader retroactively to a renderer with name: {renderer.gameObject.name}");
											renderer.sharedMaterial = matConn.mat;
											renderer.gameObject.name += " HAS MAT";
											//renderer.sharedMaterial = mat;
											//matConn.shader = shad.shader;
											//matConn.mat = renderer.sharedMaterial;
										}

										foreach (var matConn2 in AssetManager.OwnerIdToMaterial.Values.ToArray())
										{
											if (matConn2.ownerId == matConn.ownerId && matConn2.ownerId != default)
											{
												//matConn2.mat = matConn.mat;
												//matConn2.shader = matConn.shader;
												//if (matConn2.mat == null)
												//{
												//	Main.myLoggerStatic.PushMessage($"Fixing wrong mat in shader load");
												//	matConn2.mat = new Material(matConn.shader);
												//}
												//if (matConn2.mat.shader != matConn.shader)
												//{
												//	matConn2.mat.shader = matConn.shader;
												//}
												
											}
											if (matConn2.shaderFilePath == matConn.shaderFilePath)
												matConn2.ApplyChanges(new System.Collections.Generic.Queue<MaterialAction>());
											//else if (matConn2.shaderFilePath == matConn.shaderFilePath)
											//{
												//matConn2.mat = matConn.mat;
												//foreach (var rend in  matConn2.renderers)
												//{
													//rend.sharedMaterial = matConn2.mat;
													//rend.gameObject.name += " HAS NEW MAT";
												//}
											//}
											//else if (matConn2.shaderFilePath == matConn.shaderFilePath)
											//{
											//	matConn2.mat = new Material(shad.shader);
											//	foreach (var renderer in matConn2.renderers)
											//	{
											//		//if (renderer.sharedMaterial.shader == shad.shader) continue;
											//		//Main.myLoggerStatic.PushMessage($"Applying shader retroactively to a renderer with name: {renderer.gameObject.name}");
											//		renderer.sharedMaterial = matConn2.mat;
											//		renderer.gameObject.name += " HAS NEW MAT";
											//		//renderer.sharedMaterial = mat;
											//		//matConn.shader = shad.shader;
											//		//matConn.mat = renderer.sharedMaterial;
											//	}
											//}
										}

										//foreach (var rend in AssetManager.Renderers.Values)
										//{
										//	if (rend.matId == matConn.ownerId)
										//	{
										//		//var matConn3 = AssetManager.OwnerIdToMaterial[rend.matId];
										//		rend.renderer.sharedMaterial = matConn.mat;
										//	}
										//}

										//foreach (var renderer in matConn.renderers)
										//{
										//if (renderer.sharedMaterial != matConn.mat)
										//{
										//Main.myLoggerStatic.PushMessage($"Fixing wrong mat in shader load 2");
										//renderer.sharedMaterial = matConn.mat;
										//}
										//}



										//var mat = new Material(shad.shader);
										//matConn.mat = mat;


										var dupe = new GameObject("");
										dupe.transform.parent = Main.shadersRootStatic.transform;
										//dupe.transform.parent = SceneManager.GetActiveScene().GetRootGameObjects()[0].transform;
										dupe.AddComponent<MeshRenderer>().material = new Material(shad.shader);
										dupe.name = "Shader: " + FilePath;
									});
								}
								catch (Exception arg2)
								{
									Main.myLoggerStatic.PushMessage($"Exception loading shader from the loaded bundle {FilePath}\n{arg2}");
								}
							};
						}
					}
					catch (Exception arg)
					{
						Main.myLoggerStatic.PushMessage($"Exception processing loaded shader bundle for {FilePath}\n{arg}");
					}
				};
			}
			catch (Exception ex)
			{
				Main.myLoggerStatic.PushMessage("Exception loading shader from file: " + FilePath + "\n" + ex);
			}
		}

		//public static void LoadFromFileShader(LoadFromFileShaderConnector packet)
		//{
		//	Main.myLoggerStatic.PushMessage(packet.ToString());
		//	try
		//	{
		//		var bundleRequest = AssetBundle.LoadFromFileAsync(packet.File);
		//		AssetBundleRequest shaderRequest;
		//		bundleRequest.completed += delegate
		//		{
		//			try
		//			{
		//				if (bundleRequest.assetBundle == null)
		//				{
		//					Main.myLoggerStatic.PushMessage($"Could not load shader asset bundle: {packet.File}, exists: {File.Exists(packet.File)}");
		//				}
		//				else
		//				{
		//					shaderRequest = bundleRequest.assetBundle.LoadAssetAsync<Shader>(bundleRequest.assetBundle.GetAllAssetNames()[0]);
		//					shaderRequest.completed += delegate
		//					{
		//						try
		//						{
		//							if (!AssetManager.FilePathToShader.ContainsKey(packet.LocalPath))
		//							{
		//								ShaderConnector shad = new();
		//								shad.shader = shaderRequest.asset as Shader;
		//								shad.filePath = packet.File;
		//								AssetManager.FilePathToShader.Add(packet.LocalPath, shad);
		//								Main.myLoggerStatic.PushMessage($"Successfully loaded a shader from the bundle {packet.File} : {packet.LocalPath}");

		//								ShaderLoadedCallback callback = new();
		//								callback.shaderPath = packet.File;
		//								Main.QueuePacket(callback);

		//								// keep it outside of a coroutine
		//								Main.RunSynchronously(() =>
		//								//{
		//								{
		//									foreach (var matConn in AssetManager.OwnerIdToMaterial.Values)
		//									{
		//										if (matConn.shaderFilePath == packet.File)
		//										{
		//											matConn.mat.shader = shad.shader;
		//											foreach (var rend in matConn.renderers)
		//											{
		//												rend.sharedMaterial = matConn.mat;
		//											}
		//										}
		//									}

		//									var dupe = new GameObject("");
		//									//dupe.transform.parent = SceneManager.GetActiveScene().GetRootGameObjects()[0].transform;
		//									dupe.AddComponent<MeshRenderer>().material = new Material(shad.shader);
		//									dupe.name = "Shader: " + packet.LocalPath;
		//								});
		//							}
		//						}
		//						catch (Exception arg2)
		//						{
		//							Main.myLoggerStatic.PushMessage($"Exception loading shader from the loaded bundle {packet.File}\n{arg2}");
		//						}
		//					};
		//				}
		//			}
		//			catch (Exception arg)
		//			{
		//				Main.myLoggerStatic.PushMessage($"Exception processing loaded shader bundle for {packet.File}\n{arg}");
		//			}
		//		};
		//	}
		//	catch (Exception ex)
		//	{
		//		Main.myLoggerStatic.PushMessage("Exception loading shader from file: " + packet.File + "\n" + ex);
		//	}
		//}
	}

	public class LoadFromFileShaderConnector : IUpdatePacket
	{
		public string File;
		public string LocalPath;

		public int Id => (int)PacketTypes.LoadFromFileShader;

		public void Deserialize(CircularBuffer buffer)
		{
			var bytes = new byte[Constants.MAX_STRING_LENGTH];
			buffer.Read(bytes);
			File = Encoding.UTF8.GetString(bytes);

			var bytes2 = new byte[Constants.MAX_STRING_LENGTH];
			buffer.Read(bytes2);
			LocalPath = Encoding.UTF8.GetString(bytes2);
		}

		public void Serialize(CircularBuffer buffer)
		{
			buffer.Write(Encoding.UTF8.GetBytes(File));

			buffer.Write(Encoding.UTF8.GetBytes(LocalPath));
		}

		public override string ToString()
		{
			return $"LoadFromFileShaderConnector: {File} {LocalPath}";
		}
	}

	public class ShaderLoadedCallback : IUpdatePacket
	{
		public string shaderPath;
		public int Id => (int)PacketTypes.ShaderLoadedCallback;

		public void Deserialize(CircularBuffer buffer)
		{
			buffer.ReadString(out shaderPath);
		}

		public void Serialize(CircularBuffer buffer)
		{
			buffer.WriteString(shaderPath);
		}
	}
}