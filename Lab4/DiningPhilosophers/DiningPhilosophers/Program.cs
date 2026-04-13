using System;
using System.Threading;

namespace DiningPhilosophers
{
    public class PhilosopherSingle
    {
        int index;
        Thread thread;
        CancellationToken cancelToken;
        SingleCTX ctx;

        public int eatCounter = 0;
        public int thinkCounter = 0;

        public PhilosopherSingle(int n, CancellationToken token, SingleCTX ctx)
        {
            index = n;
            cancelToken = token;
            this.ctx = ctx;
        }

        public void Initialize()
        {
            thread = new Thread(Lifecycle);
            thread.Start();
        }

        private void Lifecycle()
        {
            while (!cancelToken.IsCancellationRequested)
            {
                Think();

                ctx.sharedSemaphore.WaitOne();
                try
                {
                    lock (ctx.forks[index])
                    {
                        lock (ctx.forks[(index + 1) % ctx.N])
                        {
                            Eat();
                        }
                    }
                }
                finally
                {
                    ctx.sharedSemaphore.Release();
                }

            }
        }

        private void Think()
        {
            Console.WriteLine($"Philosopher {index} is thinking");
            thinkCounter++;
            Thread.Sleep(ctx.timeout);
        }

        private void Eat()
        {

            Console.WriteLine($"Philosopher {index} eating");
            eatCounter++;
            Thread.Sleep(ctx.timeout);
        }

        public void WaitToFinish()
        {
            Console.WriteLine($"Philosopher {index} though for {thinkCounter} and ate for {eatCounter}");
            thread?.Join();
        }
    }

    public class PhilosopherMutex
    {
        int index;
        Thread thread;
        CancellationToken cancelToken;
        MutexCTX ctx;

        public int eatCounter = 0;
        public int thinkCounter = 0;

        public PhilosopherMutex(int n, CancellationToken token, MutexCTX ctx)
        {
            index = n;
            cancelToken = token;
            this.ctx = ctx;
        }

        public void Initialize()
        {
            thread = new Thread(Lifecycle);
            thread.Start();
        }

        private void Lifecycle()
        {
            while (!cancelToken.IsCancellationRequested)
            {
                Think();
                GetForks();
                Eat();
                PutForks();
            }
        }

        private void GetForks()
        {
            ctx.mutex.WaitOne();
            ctx.states[index] = "hungry";
            Test(index);
            ctx.mutex.ReleaseMutex();
            ctx.semPhil[index].WaitOne();
        }

        private void PutForks()
        {
            ctx.mutex.WaitOne();
            ctx.states[index] = "thinking";
            Test((index + ctx.N - 1) % ctx.N);
            Test((index + 1) % ctx.N);
            ctx.mutex.ReleaseMutex();
        }

        private void Test(int i)
        {
            if (ctx.states[i] == "hungry" &&
                ctx.states[(i + ctx.N - 1) % ctx.N] != "eating" &&
                ctx.states[(i + 1) % ctx.N] != "eating")
            {
                ctx.states[i] = "eating";
                try { ctx.semPhil[i].Release(); } catch (SemaphoreFullException) { }
            }
        }

        private void Think()
        {
            Console.WriteLine($"Philosopher {index} is thinking");
            thinkCounter++;
            Thread.Sleep(ctx.timeout);
        }

        private void Eat()
        {

            Console.WriteLine($"Philosopher {index} eating");
            eatCounter++;
            Thread.Sleep(ctx.timeout);
        }

        public void WaitToFinish()
        {
            Console.WriteLine($"Philosopher {index} though for {thinkCounter} and ate for {eatCounter}");
            thread?.Join();
        }
    }

    public class SingleCTX
    {
        public int N;
        public Semaphore sharedSemaphore;
        public List<object> forks;
        public int timeout;

        public SingleCTX(int n = 5, int time = 50)
        {
            N = n;
            timeout = time;
            sharedSemaphore = new Semaphore(0, N - 1);
            forks = new List<object>();
            for (int i = 0; i < N; i++)
            {
                forks.Add(new object());
            }
        }
    }

    public class MutexCTX
    {
        public int N;
        public int timeout;
        public Mutex mutex;
        public List<Semaphore> semPhil;
        public List<string> states;

        public MutexCTX(int n = 5, int time = 50)
        {
            N = n;
            timeout = time;
            mutex = new Mutex();
            semPhil = new List<Semaphore>();
            states = new List<string>();
            for (int i = 0; i < N;i++)
            {
                semPhil.Add(new Semaphore(0, 1));
                states.Add("thinking");
            }
        }
    }

    internal class Program
    {
        static void Main(string[] args)
        {
            const int N = 5;
            const int timeout = 50;
            const int timeToWork = timeout * 2 * 50;

            CancellationTokenSource cts = new CancellationTokenSource();
            CancellationToken cancelToken = cts.Token;

            string arg = Console.ReadLine();

            if (arg == "single")
            {
                var ctx = new SingleCTX(N, timeout);
                var philosophers = new List<PhilosopherSingle>();
                for (int i = 0; i < N; i++)
                {
                    philosophers.Add(new PhilosopherSingle(i, cancelToken, ctx));
                    philosophers[i].Initialize();
                }

                Thread.Sleep(1000);
                Console.WriteLine("Main released semaphore");
                ctx.sharedSemaphore.Release(N - 1);

                Thread.Sleep(timeToWork);
                cts.Cancel();
                philosophers.ForEach(p => p.WaitToFinish());
                Console.WriteLine("Done");
            }
            else
            {
                var ctx = new MutexCTX(N, timeout);
                var philosophers = new List<PhilosopherMutex>();
                for (int i = 0; i < N; i++)
                {
                    philosophers.Add(new PhilosopherMutex(i, cancelToken, ctx));
                    philosophers[i].Initialize();
                }

                Thread.Sleep(timeToWork);
                cts.Cancel();

                for (int i = 0; i < N; i++)
                    try { ctx.semPhil[i].Release(); } catch { }
                philosophers.ForEach(p => p.WaitToFinish());
                Console.WriteLine("Done");
            }

        }

        
    }
}
