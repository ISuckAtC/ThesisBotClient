using System;

public struct RTSPacket : Packet
{
    private int id;
    public bool[] selectedUnits;
    public System.Numerics.Vector2 target;

    public int Id 
    { 
        get {return id;} 
        set {id = value;}
    }

    public static RTSPacket Deserialize(byte[] bytes)
    {
        RTSPacket packet;

        packet.id = BitConverter.ToInt32(bytes, 0);
        int selectMask = BitConverter.ToInt32(bytes, 4);
        float targetX = BitConverter.ToSingle(bytes, 8);
        float targetY = BitConverter.ToSingle(bytes, 12);

        packet.selectedUnits = new bool[5];

        for (int i = 0; i < 5; ++i)
        {
            packet.selectedUnits[i] = (selectMask & (1 << i)) == (1 << i);
        }

        packet.target = new System.Numerics.Vector2(targetX, targetY);

        return packet;
    }

    public byte[] Serialize()
    {
        byte[] serialized = new byte[16];
        BitConverter.GetBytes(id).CopyTo(serialized, 0);
        
        int selectMask = 0;
        for (int i = 0; i < 5; ++i) if (selectedUnits[i]) selectMask |= (1 << i);
        BitConverter.GetBytes(selectMask).CopyTo(serialized, 4);

        BitConverter.GetBytes(target.X).CopyTo(serialized, 8);
        BitConverter.GetBytes(target.Y).CopyTo(serialized, 12);

        return serialized;
    }
}
