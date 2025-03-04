using SharedMemory;

public interface IUpdatePacket
{
	public int Id {get;}
	public void Serialize(CircularBuffer buffer);
	public void Deserialize(CircularBuffer buffer);
}