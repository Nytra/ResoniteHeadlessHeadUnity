using System.IO;

public interface IUpdatePacket
{
	public void Serialize(BinaryWriter bw);
	public void Deserialize(BinaryReader br);
}