using System;

public struct UpdatePacket : Packet
{
    private int id;
    public System.Numerics.Vector3 position;

    public int Id 
    { 
        get {return id;} 
        set {id = value;}
    }

    public static UpdatePacket Deserialize(byte[] bytes)
    {
        UpdatePacket packet;

        packet.id = BitConverter.ToInt32(bytes, 0);
        float posX = BitConverter.ToInt32(bytes, 4);
        float posY = BitConverter.ToSingle(bytes, 8);
        float posZ = BitConverter.ToSingle(bytes, 12);

        packet.position = new System.Numerics.Vector3(posX, posX, posZ);

        return packet;
    }

    public byte[] Serialize()
    {
        byte[] serialized = new byte[16];
        BitConverter.GetBytes(id).CopyTo(serialized, 0);

        BitConverter.GetBytes(position.X).CopyTo(serialized, 4);
        BitConverter.GetBytes(position.Y).CopyTo(serialized, 8);
        BitConverter.GetBytes(position.Z).CopyTo(serialized, 12);

        return serialized;
    }
}
