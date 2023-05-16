public interface Packet
{
    int Id {get; set;}
    byte[] Serialize();
}
