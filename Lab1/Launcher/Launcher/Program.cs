using System.Collections.Concurrent;
using System.Diagnostics;

namespace Launcher
{
    internal class Program
    {
        static ConcurrentDictionary<int, int> messageCounts = new();
        static int leaderId;
        static void Main(string[] args)
        {
            Console.WriteLine("Enter amount of nodes");
            string input = Console.ReadLine();
            int num;

            if (!Int32.TryParse(input, out num))
            {
                return;
            }
            int N = num;

            List<int> range = Enumerable.Range(1, N).ToList();

            System.Random random = new System.Random();
            List<int> pids = range.OrderBy(x => random.Next()).ToList();

            var processes = new List<Process>();

            for (int i = 0; i < N; i++)
            {
                int pid = pids[i];
                int left = pids[(i - 1 + N) % N];
                int right = pids[(i + 1) % N];

                var processInfo = new ProcessStartInfo
                {
                    FileName = "Node.exe",
                    Arguments = $"{pid} {left} {right}",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = false
                };

                var process = new Process { StartInfo = processInfo };

                process.OutputDataReceived += (sender, info) =>
                {
                    if (!string.IsNullOrEmpty(info.Data))
                    {
                        Console.WriteLine($"[Node {pid}]: {info.Data}");

                        if (info.Data.StartsWith("Sent:"))
                        {
                            string countStr = info.Data.Split(':')[1].Trim();
                            if (int.TryParse(countStr, out int count))
                            {
                                messageCounts.AddOrUpdate(pid, count, (k, v) => count);
                            }
                        }

                        if (info.Data.EndsWith("is a Leader"))
                        {
                            string leaderStr = info.Data.Split(' ')[0].Trim();
                            if (int.TryParse (leaderStr, out int leader))
                            {
                                leaderId = leader; 
                            }
                        }
                    }
                };

                process.Start();
                process.BeginOutputReadLine();
                processes.Add(process);

                Console.WriteLine($"Started Node {pid} (L={left}, R={right})");
            }

            foreach (var p in processes)
            {
                p.WaitForExit();
            }

            Console.WriteLine("\n--- Stats ---");
            int totalNetworkMessages = 0;
            foreach (var kvp in messageCounts)
            {
                Console.WriteLine($"Node {kvp.Key} : {kvp.Value} messages.");
                totalNetworkMessages += kvp.Value;
            }
            Console.WriteLine($"Total Messages: {totalNetworkMessages}");
            Console.WriteLine($"Leader is: {leaderId}");

            Console.WriteLine("Press enter to stop.");
            Console.ReadLine();
        }
    }
}
