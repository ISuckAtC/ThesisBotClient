using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

public enum PacketType
{
    rts,
    input,
    update
}

public class Network
{
    private TcpClient tcpClient = new TcpClient();
    private NetworkStream ns;
    private UdpClient udpClient = new UdpClient(0, AddressFamily.InterNetwork);
    private UdpClient udpAltClient = new UdpClient(0, AddressFamily.InterNetwork);
    public static int matchMakingPort;
    public static bool usingAltRecieve;

    public delegate void OnPacketRecieveHandler(Packet packet);
    public event OnPacketRecieveHandler? onPacketRecieve;
    public bool running = false;
    public static int localTickrate;
    public static PacketType packetType;
    public Packet sendPacket = new RTSPacket();
    public int distanceLatency;
    public static int PacketSize
    {
        get
        {
            switch (packetType)
            {
                case PacketType.rts: return 16;
                default: return -1;
            }
        }
    }
    public int localId;
    public int playerCount;
    public bool playerCountChanged = false;
    public int sendPort;
    public int recievePort;
    public long packetId;
    public Dictionary<long, DateTimeOffset> packetTimes = new Dictionary<long, DateTimeOffset>();
    public List<double> latencyTimes = new List<double>();

    public void Connect()
    {
        Task connection = Task.Run(async () => await ConnectAsync());
    }

    public async Task ConnectAsync()
    {
        try
        {
            await tcpClient.ConnectAsync("127.0.0.1", matchMakingPort);

            NetworkStream ns = tcpClient.GetStream();

            byte[] buffer = new byte[17];

            await ns.ReadAsync(buffer, 0, 17);

            if (buffer[0] == 1)
            {
                Console.WriteLine("Recieved OK input from server");
            }

            playerCount = BitConverter.ToInt32(buffer, 1);
            playerCountChanged = true;
            localId = BitConverter.ToInt32(buffer, 5);

            sendPort = BitConverter.ToInt32(buffer, 9);
            recievePort = BitConverter.ToInt32(buffer, 13);

            sendPacket.Id = localId;

            Console.WriteLine(
                "playerCount: " + playerCount +
                "\nlocalId: " + localId +
                "\nsendPort: " + sendPort +
                "\nrecievePort: " + recievePort);

            udpClient = new UdpClient(0, AddressFamily.InterNetwork);
            udpClient.Connect("127.0.0.1", sendPort);
            if (sendPort != recievePort)
            {
                udpAltClient.Connect("127.0.0.1", recievePort);
                usingAltRecieve = true;
            }

            buffer = BitConverter.GetBytes(((IPEndPoint)udpClient.Client.LocalEndPoint).Port);

            await ns.WriteAsync(buffer, 0, 4);

            buffer = new byte[1];

            await ns.ReadAsync(buffer, 0, 1);

            running = true;

            Console.WriteLine("Running...");

            Task sendLoop = Task.Run(async () => await SendLoop());
            Task recieveLoop = Task.Run(async () => await RecieveLoop());
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message + "\n\n" + e.StackTrace);
        }
    }

    public async Task OverheadRecieveLoop()
    {
        byte[] buffer = new byte[4];
        await ns.ReadAsync(buffer, 0, 4);
        int newPlayerCount = BitConverter.ToInt32(buffer, 0);
        playerCount = newPlayerCount;
        playerCountChanged = true;
    }

    public async Task SendLoop()
    {
        try
        {
            while (running)
            {
                Task waitTask = Task.Delay(localTickrate);

                packetTimes.Add(packetId, DateTimeOffset.Now);

                byte[] send = new byte[PacketSize + 4 + 8];
                BitConverter.GetBytes(distanceLatency).CopyTo(send, 0);
                BitConverter.GetBytes(packetId).CopyTo(send, 4);
                packetId++;
                sendPacket.Serialize().CopyTo(send, 12);
                await udpClient.SendAsync(send, send.Length);

                await waitTask;
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message + "\n\n" + e.StackTrace);
        }
    }

    public async Task RecieveLoop()
    {
        while (running)
        {
            Task waitTask = Task.Delay(localTickrate);

            UdpReceiveResult result = await (usingAltRecieve ? udpAltClient : udpClient).ReceiveAsync();

            for (int i = 0; i < playerCount; ++i)
            {
                int skip = (i * (PacketSize + 8));
                byte[] buffer = new byte[PacketSize];
                long returnedPacketId = BitConverter.ToInt64(result.Buffer, skip);
                int senderId = BitConverter.ToInt32(result.Buffer, skip + 8);

                if (senderId == localId)
                {
                    TimeSpan latency = DateTimeOffset.Now - packetTimes[returnedPacketId];

                    latencyTimes.Add(latency.TotalMilliseconds);
                    if (latencyTimes.Count > 60)
                    {
                        latencyTimes.RemoveAt(0);
                    }
                }
                Array.Copy(result.Buffer, skip + 8, buffer, 0, PacketSize);
                switch (packetType)
                {
                    case PacketType.rts:
                        {
                            onPacketRecieve?.Invoke(RTSPacket.Deserialize(buffer));
                            break;
                        }
                }
            }
            await waitTask;
        }
    }
}