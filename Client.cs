using System;
using System.Numerics;

public class Client
{
    NewNetwork network;

    public Dictionary<int, List<RTSSolider>> soliderGroups = new Dictionary<int, List<RTSSolider>>();
    public Dictionary<int, UpdateSolider> updateSoliders = new Dictionary<int, UpdateSolider>();
    public Dictionary<int, InputSolider> inputSoliders = new Dictionary<int, InputSolider>();
    public Dictionary<int, UpdateSolider> controlledSoliders = new Dictionary<int, UpdateSolider>();

    private bool objectsSpawned = false;

    public int distanceLatency;

    private bool setup = true;

    public delegate void UpdateHandler();
    public event UpdateHandler? onUpdate;

    private int inputFrequency;
    private int inputWait;



    public Client(int distanceLatency, int inputFrequency, int startingWait, NetworkMode networkMode, PacketType packetType)
    {
        this.distanceLatency = distanceLatency;
        this.inputFrequency = inputFrequency;
        this.inputWait = startingWait;
        network = new NewNetwork();
        network.distanceLatency = distanceLatency;
        network.networkMode = networkMode;
        network.packetType = packetType;

        if (packetType == PacketType.input)
        {
            InputPacket packet = new InputPacket();
            packet.analog = new Vector2(0, 0);
            packet.buttons = new bool[32];
            network.sendPacket = packet;
        }
        if (packetType == PacketType.update)
        {
            UpdatePacket packet = new UpdatePacket();
            packet.position = Vector3.Zero;
            network.sendPacket = packet;
        }
        network.Connect();
    }

    void OnPacketRecieve(Packet packet)
    {
        try
        {
            if (network.networkMode == NetworkMode.servercontrol)
            {
                UpdatePacket updatePacket = (UpdatePacket)packet;

                if (!controlledSoliders.ContainsKey(updatePacket.Id))
                {
                    UpdateSolider newSolider = new UpdateSolider(updatePacket.Id, updatePacket.position);
                    controlledSoliders.Add(updatePacket.Id, newSolider);
                }

                UpdateSolider solider = controlledSoliders[updatePacket.Id];
                solider.position = updatePacket.position;
            }
            else switch (network.packetType)
                {
                    case PacketType.update:
                        {
                            UpdatePacket updatePacket = (UpdatePacket)packet;

                            if (updateSoliders.ContainsKey(updatePacket.Id))
                            {
                                if (updatePacket.Id != network.localId)
                                {
                                    updateSoliders[updatePacket.Id].position = updatePacket.position;
                                }
                            }
                            break;
                        }
                    case PacketType.rts:
                        {
                            RTSPacket rTSPacket = (RTSPacket)packet;

                            if (!soliderGroups.ContainsKey(rTSPacket.Id)) return;

                            List<RTSSolider> group = soliderGroups[rTSPacket.Id];

                            for (int i = 0; i < 5; ++i)
                            {
                                if (rTSPacket.selectedUnits[i]) group[i].SetTarget(rTSPacket.target);
                            }
                            break;
                        }
                    case PacketType.input:
                        {
                            InputPacket inputPacket = (InputPacket)packet;

                            InputSolider solider = inputSoliders[inputPacket.Id];
                            solider.Input(inputPacket.buttons, inputPacket.analog);

                            break;
                        }
                }


        }
        catch (System.Exception e)
        {
            Console.WriteLine(e.Message + "\n\n" + e.StackTrace);
        }
    }

    public double GetDiagnostics()
    {
        double output;

        double average = 0;
        for (int i = 0; i < network.latencyTimes.Count; ++i) average += network.latencyTimes[i];
        average /= network.latencyTimes.Count;

        output = average;
        
        return output;
    }

    public void Update()
    {
        
        

        try
        {
            if (!setup && inputWait-- < 0)
            {
                inputWait = Program.random.Next(inputFrequency, inputFrequency * 2);

                switch (network.packetType)
                {
                    case PacketType.update:
                        {
                            Vector3 targetPosition = new Vector3((Program.random.NextSingle() - 0.5f) * 20f, 0f, (Program.random.NextSingle() - 0.5f) * 20f);

                            UpdateSolider ourSolider = updateSoliders[network.localId];

                            Vector3 direction = targetPosition - ourSolider.position;
                            direction.Y = 0;
                            direction = Vector3.Normalize(direction);

                            Vector3 movement = direction * UpdateSolider.speed;

                            ourSolider.position += movement;

                            UpdatePacket currentPacket = (UpdatePacket)network.sendPacket;

                            //Console.WriteLine(currentPacket.position + " | " + ourSolider.position);

                            currentPacket.position = ourSolider.position;

                            network.sendPacket = currentPacket;

                            break;
                        }
                    case PacketType.rts:
                        {
                            Vector2 targetPosition = new Vector2((Program.random.NextSingle() - 0.5f) * 20f, (Program.random.NextSingle() - 0.5f) * 20f);
                            RTSPacket currentPacket = (RTSPacket)network.sendPacket;
                            currentPacket.selectedUnits[0] = true;
                            currentPacket.target = targetPosition;
                            network.sendPacket = currentPacket;
                            break;
                        }
                    case PacketType.input:
                        {
                            Vector2 analog = new Vector2((Program.random.NextSingle() - 0.5f) * 2f, (Program.random.NextSingle() - 0.5f) * 2f);
                            bool[] buttons = new bool[32];
                            for (int i = 0; i < 32; ++i)
                            {
                                buttons[i] = Program.random.Next(0, 2) == 0;
                            }

                            InputPacket currentPacket = (InputPacket)network.sendPacket;
                            currentPacket.analog = analog;
                            currentPacket.buttons = buttons;
                            network.sendPacket = currentPacket;

                            break;
                        }
                }
            }


            onUpdate?.Invoke();

            if (network.networkMode == NetworkMode.servercontrol)
            {
                if (network.running && setup)
                {
                    switch (network.packetType)
                    {
                        case PacketType.update:
                            {
                                UpdateSolider solider = new UpdateSolider(network.localId, new Vector3(0f, 1f, 0f));
                                updateSoliders.Add(network.localId, solider);
                                break;
                            }
                        case PacketType.rts:
                            {
                                List<RTSSolider> soliders = new List<RTSSolider>();
                                for (int k = 0; k < 32; ++k)
                                {
                                    RTSSolider soliderScript = new RTSSolider(Vector2.Zero, 1f, this);
                                    soliders.Add(soliderScript);
                                }

                                soliderGroups.Add(network.localId, soliders);
                                break;
                            }
                        case PacketType.input:
                            {
                                InputSolider solider = new InputSolider(network.localId, new Vector3(0f, 1f, 0f));
                                inputSoliders.Add(network.localId, solider);
                                break;
                            }
                    }
                    Console.WriteLine("Initial setup");
                    network.onPacketRecieve += OnPacketRecieve;
                    setup = false;
                }
                return;
            }

            if (network.playerCountChanged)
            {
                if (setup)
                {
                    Console.WriteLine("Initial setup");
                    network.onPacketRecieve += OnPacketRecieve;
                    setup = false;
                }
                else
                {
                    Console.WriteLine("Updated playercount");
                }
                network.playerCountChanged = false;

                switch (network.packetType)
                {
                    case PacketType.rts:
                        {
                            for (int i = 0; i < soliderGroups.Count; ++i)
                            {
                                soliderGroups[i].Clear();
                            }
                            soliderGroups.Clear();

                            for (int i = 0; i < network.playerCount; ++i)
                            {
                                List<RTSSolider> soliders = new List<RTSSolider>();
                                for (int k = 0; k < 32; ++k)
                                {
                                    RTSSolider soliderScript = new RTSSolider(Vector2.Zero, 1f, this);
                                    soliders.Add(soliderScript);
                                }

                                soliderGroups.Add(i, soliders);
                            }
                            break;
                        }
                    case PacketType.update:
                        {
                            if (updateSoliders.ContainsKey(network.localId)) onUpdate -= updateSoliders[network.localId].Update;
                            updateSoliders.Clear();

                            for (int i = 0; i < network.playerCount; ++i)
                            {
                                UpdateSolider soliderScript = new UpdateSolider(i, new Vector3(0f, 1f, 0f));
                                if (soliderScript.id == network.localId) onUpdate += soliderScript.Update;

                                updateSoliders.Add(soliderScript.id, soliderScript);
                            }
                            break;
                        }
                    case PacketType.input:
                        {
                            if (inputSoliders.ContainsKey(network.localId)) onUpdate -= inputSoliders[network.localId].Update;
                            inputSoliders.Clear();

                            for (int i = 0; i < network.playerCount; ++i)
                            {
                                InputSolider soliderScript = new InputSolider(i, new Vector3(0f, 1f, 0f));
                                if (soliderScript.id == network.localId) onUpdate += soliderScript.Update;

                                inputSoliders.Add(soliderScript.id, soliderScript);
                            }
                            break;
                        }
                }


            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message + "\n\n" + e.StackTrace);
        }
    }
}