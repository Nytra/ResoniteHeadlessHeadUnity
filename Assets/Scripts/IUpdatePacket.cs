using SharedMemory;
using System.IO;

public interface IUpdatePacket
{
	public void Serialize(CircularBuffer buffer);
	public void Deserialize(CircularBuffer buffer);
}