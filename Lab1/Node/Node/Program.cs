using System.IO;
using System.IO.Pipes;
using System.Numerics;
using System.Reflection;
using System.Text.Json;
using System.Threading.Channels;

namespace Node
{
    internal class Program
    {
        static bool Status; // Is active participant;
        static int k;       // Phase
        static int replyCount;
        static int msgCount;
        static SemaphoreSlim _pipeLock = new SemaphoreSlim(1, 1);
        static async Task Main(string[] args)
        { 
            int PID = int.Parse(args[0]);
            int leftId = int.Parse(args[1]);
            int rightId = int.Parse(args[2]);
            Status = true; 
            k = 0; 
            replyCount = 0;

            string inLeftPipe = $"Pipe_{leftId}_{PID}";
            string inRightPipe = $"Pipe_{rightId}_{PID}";
            string outLeftPipe = $"Pipe_{PID}_{leftId}";
            string outRightPipe = $"Pipe_{PID}_{rightId}";

            using var inLeft = new NamedPipeServerStream(inLeftPipe, PipeDirection.In);
            using var inRight = new NamedPipeServerStream(inRightPipe, PipeDirection.In);
            using var outLeft = new NamedPipeClientStream(".", outLeftPipe, PipeDirection.Out);
            using var outRight = new NamedPipeClientStream(".", outRightPipe, PipeDirection.Out);

            await Task.WhenAll(
                inLeft.WaitForConnectionAsync(),
                inRight.WaitForConnectionAsync(),
                outLeft.ConnectAsync(),
                outRight.ConnectAsync()
            );

            var channel = Channel.CreateUnbounded<Message>();

            _ = ListenAsync(inLeft, channel);
            _ = ListenAsync(inRight, channel);

            using var leftWriter = new StreamWriter(outLeft) { AutoFlush = true };
            using var rightWriter = new StreamWriter(outRight) { AutoFlush = true };

            _ = Task.Run(async () =>
            {
                await foreach (var msg in channel.Reader.ReadAllAsync())
                {
                    await HSLogic(msg, PID, leftWriter, rightWriter);
                }
            });

            await Task.Delay(TimeSpan.FromSeconds(2));

            if (Status)
            {
                await StartPhase(k, PID, leftWriter, rightWriter);
            }

            await Task.Delay(TimeSpan.FromSeconds(4));
            Console.WriteLine($"Sent: {msgCount}");

            Console.WriteLine($"Node {PID} time limit reached. Exiting.");
        }

        static async Task ListenAsync(Stream stream, ChannelWriter<Message> writer)
        {
            using var reader = new StreamReader(stream);
            while (true)
            {
                string? json = await reader.ReadLineAsync();
                if (json == null) break;
                if (string.IsNullOrWhiteSpace(json)) continue;
                try
                {
                    var msg = JsonSerializer.Deserialize<Message>(json);
                    if (msg != null)
                    {
                        //Console.WriteLine($"Received {msg.MsgType} from {msg.SenderPID}");
                        await writer.WriteAsync(msg);
                    }
                }
                catch (JsonException ex)
                {
                    Console.WriteLine($"Json Exception caught: {ex.Message}");
                }
            }
        }

        static async Task SendAsync(StreamWriter writer, Message msg)
        {
            string json = JsonSerializer.Serialize(msg);
            msgCount += 1;

            await _pipeLock.WaitAsync();
            try
            {
                await writer.WriteLineAsync(json);
            }
            finally
            {
                _pipeLock.Release();
            }
        }

        static async Task HSLogic(Message msg, int PID, StreamWriter leftWriter, StreamWriter rightWriter)
        {
            if(msg.MsgType == "Reply")
            {
                if (msg.SenderPID == PID)
                {
                    if (!Status) return;
                    replyCount++;

                    if (replyCount == 2)
                    {
                        replyCount = 0;
                        k++;
                        await StartPhase(k, PID, leftWriter, rightWriter);
                    }
                }
                else
                {
                    if (msg.Direction == "Left") await SendAsync(leftWriter, msg);
                    else if (msg.Direction == "Right") await SendAsync(rightWriter, msg);
                }

                return;
            }

            if (msg.MsgType == "Ask" && msg.SenderPID == PID)
            {
                Console.WriteLine($"{PID} is a Leader");
                return;
            }    

            if (!Status || msg.SenderPID > PID)
            {
                Status = false;
                if(msg.Lifetime > 1)
                {
                    msg.Lifetime--;
                    if (msg.Direction == "Left") await SendAsync(leftWriter, msg);
                    else if (msg.Direction == "Right") await SendAsync(rightWriter, msg);
                }
                else
                {
                    Message reply = new Message(msg.SenderPID, 0, msg.Phase, Reverse(msg.Direction), "Reply");
                    if (reply.Direction == "Left") await SendAsync(leftWriter, reply);
                    else if (reply.Direction == "Right") await SendAsync(rightWriter, reply);
                }
            }
            else if (msg.SenderPID < PID) 
            {
                return;
            }
        }

        static async Task StartPhase(int phase, int PID, StreamWriter leftWriter, StreamWriter rightWriter)
        {
            Console.WriteLine($"{PID}, started phase {phase}, Status: {Status}");
            int distance = (int)MathF.Pow(2, phase);

            Message leftMsg = new Message(PID, distance, phase, "Left", "Ask");
            Message rightMsg = new Message(PID, distance, phase, "Right", "Ask");

            await SendAsync(leftWriter, leftMsg);
            await SendAsync(rightWriter, rightMsg);
        }

        static string Reverse(string Direction)
        {
            if (Direction == "Left") return "Right";
            if (Direction == "Right") return "Left";
            return "";
        }
    }

    public class Message
    {
        public int SenderPID { get; set; }
        public int Lifetime { get; set; }
        public int Phase { get; set; }
        public string Direction { get; set; } // Left or Right
        public string MsgType { get; set; } // Ask or Reply

        public Message() { }

        public Message(int pid, int distance, int phase, string dir, string type)
        {
            SenderPID = pid;
            Lifetime = distance;
            Phase = phase;
            Direction = dir;
            MsgType = type;
        }
    }
}
