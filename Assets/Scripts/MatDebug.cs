using UnityEngine;
using System.Collections.Generic;

namespace Thundagun
{
	public class MatDebug : MonoBehaviour
	{
		public List<MeshRenderer> TheList = new();
		public List<SkinnedMeshRenderer> TheSkinnedList = new();
		public MaterialConnector matConn;
		public int actionQueueCount;
		public string shaderLocalPath;
		public string shaderFilePath;
		public ulong ownerId;
		//public Dictionary<string, float> test = new();

		private void Update()
		{
			if (matConn != null)
			{
				actionQueueCount = matConn.mainActionQueue.Count;
				shaderLocalPath = matConn.shaderLocalPath;
				shaderFilePath = matConn.shaderFilePath;
				ownerId = matConn.ownerId;
			}
		}
	}
}