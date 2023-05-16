public class Program
{
    public static Random random;
    public static List<Client> clients = new List<Client>();
    public static List<(double, int)>[] diagnostics;
    public static async Task Main(string[] args)
    {
        random = new Random();
        Network.localTickrate = 33;
        Network.matchMakingPort = 6969;
        Network.usingAltRecieve = false;


        for (int i = 0; i < 10; ++i)
        {
            Client client = new Client(0, 10, 60 + (i), NetworkMode.servercontrol, PacketType.update);
            clients.Add(client);
        }

        diagnostics = new List<(double, int)>[clients.Count];
        Array.Fill(diagnostics, new List<(double, int)>());

        Task diag = Task.Run(async () => await SaveDiagnostics(100, 200, 10000));

        while(true)
        {
            Task wait = Task.Delay((int)(1000 / 60));
            for (int i = 0; i < clients.Count; ++i) clients[i].Update();
            await wait;
        }
    }

    public static async Task SaveDiagnostics(int interval, int captureAmount, int delay)
    {
        try
        {
        await Task.Delay(delay);

        List<double> latencyAverages = new List<double>();

        for (int i = 0; i < captureAmount; ++i)
        {
            Task wait = Task.Delay(interval);
            double pingAverage = 0;
            for (int k = 0; k < clients.Count; ++k)
            {
                var d = clients[k].GetDiagnostics();
                pingAverage += d;
            }
            latencyAverages.Add(pingAverage / clients.Count);
            await wait;
        }

        string pingAverages = "";

        for (int i = 0; i < captureAmount; ++i)
        {
            pingAverages += latencyAverages[i].ToString() + ";";
        }

        File.WriteAllText("../outputs/outClient.csv", pingAverages);
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message + "\n\n" + e.StackTrace);
        }
    }
}