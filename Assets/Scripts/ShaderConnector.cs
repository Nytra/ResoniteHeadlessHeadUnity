using SharedMemory;
using System.Text;
using UnityEngine;

namespace Thundagun
{
	public class ShaderConnector
	{
		public Shader shader;
	}

	public class LoadFromFileShaderConnector : IUpdatePacket
	{
		public string File;
		public string LocalPath;

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
}