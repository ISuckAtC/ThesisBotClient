using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

public enum NetworkMode
{
    servercontrol,
    serverpass
}

public class NewNetwork
{
    private TcpClient tcpClient = new TcpClient();
    private NetworkStream ns;
    private Dictionary<int, UdpClient> udpClients = new Dictionary<int, UdpClient>();
    private UdpClient mainUdpClient;
    public static int matchMakingPort = 6969;

    public delegate void OnPacketRecieveHandler(Packet packet);

    public event OnPacketRecieveHandler onPacketRecieve;
    public bool running = false;
    public static int localTickrate = 33;
    public PacketType packetType;
    public NetworkMode networkMode;
    public Packet sendPacket;
    public List<RTSPacket> queuedActions = new List<RTSPacket>();
    public int distanceLatency;
    public int PacketSize
    {
        get
        {
            switch (packetType)
            {
                case PacketType.rts: return 16;
                case PacketType.update: return 16;
                case PacketType.input: return 16;
                default: return -1;
            }
        }
    }
    public int localId;
    public static int hardPlayerCount = 2;
    public long[] lowestLastRecieved = new long[hardPlayerCount - 1];
    public int playerCount;
    public bool playerCountChanged = false;
    public int sendPort;
    public long packetId = 0;
    public long LowestLastReceived
    {
        get
        {
            if (hardPlayerCount < 2) throw new Exception("?");
            long lowest = long.MaxValue;
            for (int i = 0; i < lowestLastRecieved.Length; ++i) if (lowestLastRecieved[i] < lowest) lowest = lowestLastRecieved[i];
            return lowest;
        }
    }
    public Dictionary<long, DateTimeOffset> packetTimes = new Dictionary<long, DateTimeOffset>();
    public List<double> latencyTimes = new List<double>();

    public void Connect()
    {
        Task connection = Task.Run(async () => await ConnectAsync());
    }

    public async Task ConnectAsyncServercontrol()
    {
        byte[] buffer = new byte[17];

        await ns.ReadAsync(buffer, 0, 17);

        if (buffer[0] == 1)
        {
            Console.WriteLine("Recieved OK input from server");
        }

        playerCount = BitConverter.ToInt32(buffer, 1);
        playerCountChanged = true;
        localId = BitConverter.ToInt32(buffer, 5);

        Console.WriteLine("I am id: " + localId);

        sendPort = BitConverter.ToInt32(buffer, 9);

        if (packetType != PacketType.rts) sendPacket.Id = localId;

        //udpClients.Add(new UdpClient(0, AddressFamily.InterNetwork));
        mainUdpClient.Connect("127.0.0.1", sendPort);

        buffer = BitConverter.GetBytes(((IPEndPoint)mainUdpClient.Client.LocalEndPoint).Port);

        await ns.WriteAsync(buffer, 0, 4);

        buffer = new byte[1];

        await ns.ReadAsync(buffer, 0, 1);

        running = true;

        Task sendLoop = Task.Run(async () => await SendLoop());
        Task recieveLoop = Task.Run(async () => await RecieveLoop());
    }

    public async Task ConnectAsyncServerpass()
    {
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

        if (packetType != PacketType.rts) sendPacket.Id = localId;

        //udpClients.Add(new UdpClient(0, AddressFamily.InterNetwork));
        mainUdpClient.Connect("127.0.0.1", sendPort);

        buffer = BitConverter.GetBytes(((IPEndPoint)mainUdpClient.Client.LocalEndPoint).Port);

        await ns.WriteAsync(buffer, 0, 4);

        buffer = new byte[1];

        await ns.ReadAsync(buffer, 0, 1);

        running = true;

        Task sendLoop = Task.Run(async () => await SendLoop());
        Task recieveLoop = Task.Run(async () => await RecieveLoop());
    }

    public async Task ConnectAsync()
    {
        try
        {
            await tcpClient.ConnectAsync("127.0.0.1", matchMakingPort);

            ns = tcpClient.GetStream();

            mainUdpClient = new UdpClient(0, AddressFamily.InterNetwork);

            switch (networkMode)
            {
                case NetworkMode.serverpass:
                    {
                        await ConnectAsyncServerpass();
                        return;
                    }
                case NetworkMode.servercontrol:
                    {
                        await ConnectAsyncServercontrol();
                        return;
                    }
            }
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
                //Console.WriteLine("packettime added");
                byte[] send;

                switch (packetType)
                {
                    case PacketType.rts:
                        {
                            send = new byte[4 + 4 + 4 + 8 + (PacketSize * queuedActions.Count)];
                            BitConverter.GetBytes(packetId).CopyTo(send, 4);
                            BitConverter.GetBytes(localId).CopyTo(send, 12);
                            BitConverter.GetBytes(queuedActions.Count).CopyTo(send, 16);
                            for (int i = 0; i < queuedActions.Count; ++i)
                            {
                                queuedActions[i].Serialize().CopyTo(send, 20 + (i * PacketSize));
                            }
                            break;
                        }
                    case PacketType.input:
                        {
                            send = new byte[4 + 8 + PacketSize];
                            BitConverter.GetBytes(packetId).CopyTo(send, 4);
                            sendPacket.Serialize().CopyTo(send, 12);
                            break;
                        }
                    case PacketType.update:
                        {
                            send = new byte[4 + 8 + PacketSize];
                            BitConverter.GetBytes(packetId).CopyTo(send, 4);
                            sendPacket.Serialize().CopyTo(send, 12);
                            break;
                        }
                    default:
                        {
                            send = new byte[4];
                            break;
                        }
                }

                BitConverter.GetBytes(distanceLatency).CopyTo(send, 0);

                await mainUdpClient.SendAsync(send, send.Length);

                packetId++;

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
        try
        {
            while (running)
            {
                if (networkMode == NetworkMode.servercontrol)
                {
                    Task waitTask = Task.Delay(localTickrate);

                    UdpReceiveResult result = await mainUdpClient.ReceiveAsync();

                    int updateCount = BitConverter.ToInt32(result.Buffer, 0);
                    int idCount = BitConverter.ToInt32(result.Buffer, 4);

                    for (int i = 0; i < updateCount; ++i)
                    {
                        byte[] packetBuffer = new byte[16];
                        Array.Copy(result.Buffer, 8 + (16 * i), packetBuffer, 0, 16);
                        UpdatePacket packet = UpdatePacket.Deserialize(packetBuffer);

                        onPacketRecieve?.Invoke(packet);
                    }

                    for (int i = 0; i < idCount; ++i)
                    {
                        int senderId = BitConverter.ToInt32(result.Buffer, 8 + (16 * updateCount) + (12 * i));
                        int packetId = BitConverter.ToInt32(result.Buffer, 8 + (16 * updateCount) + (12 * i) + 4);

                        if (senderId == localId)
                        {
                            //Console.WriteLine("senderId: " + senderId + " | packetId: " + packetId);
                            //for (int k = 0; k < packetTimes.Count; ++k) Console.WriteLine(packetTimes.ElementAt(k).Key + " | " + packetTimes.ElementAt(k).Value);
                            lock (packetTimes)
                            {
                                if (packetTimes.ContainsKey(packetId) && packetId != 0)
                                {
                                    TimeSpan latency = DateTimeOffset.Now - packetTimes[packetId];
                                    //Console.WriteLine(packetId + " | " + latency.TotalMilliseconds);
                                    latencyTimes.Add(latency.TotalMilliseconds);
                                    if (latencyTimes.Count > 60)
                                    {
                                        latencyTimes.RemoveAt(0);
                                    }
                                }
                            }

                        }
                    }

                    await waitTask;
                }
                if (networkMode == NetworkMode.serverpass)
                {
                    Task waitTask = Task.Delay(localTickrate);

                    UdpReceiveResult result = await mainUdpClient.ReceiveAsync();

                    int playerCount = BitConverter.ToInt32(result.Buffer, 0);

                    int skip = 4;

                    for (int i = 0; i < playerCount; ++i)
                    {
                        long packetId = BitConverter.ToInt64(result.Buffer, skip);
                        int senderId = BitConverter.ToInt32(result.Buffer, skip + 8);

                        if (senderId == localId)
                        {
                            TimeSpan latency = DateTimeOffset.Now - packetTimes[packetId];

                            latencyTimes.Add(latency.TotalMilliseconds);
                            if (latencyTimes.Count > 60)
                            {
                                latencyTimes.RemoveAt(0);
                            }
                            packetTimes.Remove(packetId);
                        }

                        switch (packetType)
                        {
                            case PacketType.rts:
                                {
                                    int packetCount = BitConverter.ToInt32(result.Buffer, 12);

                                    for (int k = 0; k < packetCount; ++k)
                                    {
                                        byte[] packetBuffer = new byte[16];
                                        Array.Copy(result.Buffer, skip + 16 + (16 * k), packetBuffer, 0, 16);
                                        RTSPacket packet = RTSPacket.Deserialize(packetBuffer);

                                        onPacketRecieve?.Invoke(packet);
                                    }

                                    skip += 24 + (16 * packetCount);
                                    break;
                                }
                            case PacketType.input:
                                {
                                    byte[] packetBuffer = new byte[16];
                                    Array.Copy(result.Buffer, skip + 8, packetBuffer, 0, 16);
                                    InputPacket packet = InputPacket.Deserialize(packetBuffer);

                                    onPacketRecieve?.Invoke(packet);
                                    skip += 24;
                                    break;
                                }
                            case PacketType.update:
                                {
                                    byte[] packetBuffer = new byte[16];
                                    Array.Copy(result.Buffer, skip + 8, packetBuffer, 0, 16);
                                    UpdatePacket packet = UpdatePacket.Deserialize(packetBuffer);

                                    onPacketRecieve?.Invoke(packet);
                                    skip += 24;
                                    break;
                                }
                        }
                    }

                    await waitTask;
                }
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message + "\n\n" + e.StackTrace);
        }
    }
}