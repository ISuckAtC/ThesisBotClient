using System;

public struct InputPacket : Packet
{
    private int id;
    public bool[] buttons;
    public System.Numerics.Vector2 analog;

    public int Id
    {
        get { return id; }
        set { id = value; }
    }

    public static InputPacket Deserialize(byte[] bytes)
    {
        InputPacket packet;

        packet.id = BitConverter.ToInt32(bytes, 0);
        int buttonMask = BitConverter.ToInt32(bytes, 4);
        float targetX = BitConverter.ToSingle(bytes, 8);
        float targetY = BitConverter.ToSingle(bytes, 12);

        packet.buttons = new bool[32];

        for (int i = 0; i < 32; ++i)
        {
            packet.buttons[i] = (buttonMask & (1 << i)) == (1 << i);
        }

        packet.analog = new System.Numerics.Vector2(targetX, targetY);

        return packet;
    }

    public byte[] Serialize()
    {
        byte[] serialized = new byte[16];
        BitConverter.GetBytes(id).CopyTo(serialized, 0);

        int buttonMask = 0;
        for (int i = 0; i < 32; ++i) if (buttons[i]) buttonMask |= (1 << i);
        BitConverter.GetBytes(buttonMask).CopyTo(serialized, 4);
        BitConverter.GetBytes(analog.X).CopyTo(serialized, 8);
        BitConverter.GetBytes(analog.Y).CopyTo(serialized, 12);

        return serialized;
    }
}
