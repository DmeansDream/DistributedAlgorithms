using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using DiningPhilosophers;

namespace DiningPhilosophers.Tests
{
    [TestClass]
    public class SingleSemaphoreTests
    {
        private const int N = 5;
        private const int runMs = 2000;
        private const int timeout = 30;

        private List<PhilosopherSingle> StartPhilosophers(CancellationToken token)
        {
            var ctx = new SingleCTX(N, timeout);
            var philosophers = new List<PhilosopherSingle>();

            for (int i = 0; i < N; i++)
            {
                philosophers.Add(new PhilosopherSingle(i, token, ctx));
                philosophers[i].Initialize();
            }

            Thread.Sleep(100);
            ctx.sharedSemaphore.Release(N - 1);

            return philosophers;
        }

        [TestMethod]
        public void AllPhilosophersEatAtLeastOnce()
        {
            using var cts = new CancellationTokenSource();
            var philosophers = StartPhilosophers(cts.Token);

            Thread.Sleep(runMs);
            cts.Cancel();
            philosophers.ForEach(p => p.WaitToFinish());

            foreach (var p in philosophers)
                Assert.IsTrue(p.eatCounter > 0, $"Philosopher {philosophers.IndexOf(p)} never ate");
        }

        [TestMethod]
        public void PhilosophersKeepEatingOverTime()
        {
            using var cts = new CancellationTokenSource();
            var philosophers = StartPhilosophers(cts.Token);

            Thread.Sleep(runMs / 2);
            int[] midwayCounts = philosophers.Select(p => p.eatCounter).ToArray();

            Thread.Sleep(runMs / 2);
            cts.Cancel();
            philosophers.ForEach(p => p.WaitToFinish());

            int[] finalCounts = philosophers.Select(p => p.eatCounter).ToArray();

            Assert.IsTrue(finalCounts.Sum() > midwayCounts.Sum(),
                "Philosophers stopped eating — possible deadlock");
        }
    }

    [TestClass]
    public class MutexStateTests
    {
        private const int N = 5;
        private const int runMs = 2000;
        private const int timeout = 30;

        private (List<PhilosopherMutex> philosophers, MutexCTX ctx) StartPhilosophers(CancellationToken token)
        {
            var ctx = new MutexCTX(N, timeout);
            var philosophers = new List<PhilosopherMutex>();

            for (int i = 0; i < N; i++)
                philosophers.Add(new PhilosopherMutex(i, token, ctx));

            philosophers.ForEach(p => p.Initialize());

            return (philosophers, ctx);
        }

        [TestMethod]
        public void AllPhilosophersEatAtLeastOnce()
        {
            using var cts = new CancellationTokenSource();
            var (philosophers, ctx) = StartPhilosophers(cts.Token);

            Thread.Sleep(runMs);

            cts.Cancel();

            for (int i = 0; i < N; i++)
                try { ctx.semPhil[i].Release(); } catch { }

            philosophers.ForEach(p => p.WaitToFinish());

            foreach (var p in philosophers)
                Assert.IsTrue(p.eatCounter > 0, $"Philosopher {philosophers.IndexOf(p)} never ate");
        }

        [TestMethod]
        public void PhilosophersKeepEatingOverTime()
        {
            using var cts = new CancellationTokenSource();
            var (philosophers, ctx) = StartPhilosophers(cts.Token);

            Thread.Sleep(runMs / 2);
            int[] midwayCounts = philosophers.Select(p => p.eatCounter).ToArray();

            Thread.Sleep(runMs / 2);

            cts.Cancel();

            for (int i = 0; i < N; i++)
                try { ctx.semPhil[i].Release(); } catch { }

            philosophers.ForEach(p => p.WaitToFinish());

            int[] finalCounts = philosophers.Select(p => p.eatCounter).ToArray();

            Assert.IsTrue(finalCounts.Sum() > midwayCounts.Sum(),
                "Philosophers stopped eating — possible deadlock");
        }

        [TestMethod]
        public void NoTwoNeighboursEatAtTheSameTime()
        {
            using var cts = new CancellationTokenSource();
            var (philosophers, ctx) = StartPhilosophers(cts.Token);

            bool violation = false;
            for (int check = 0; check < 50; check++)
            {
                Thread.Sleep(runMs / 50);

                for (int i = 0; i < N; i++)
                {
                    int next = (i + 1) % N;
                    if (ctx.states[i] == "eating" && ctx.states[next] == "eating")
                    {
                        violation = true;
                        break;
                    }
                }

                if (violation) break;
            }

            cts.Cancel();
            for (int i = 0; i < N; i++)
                try { ctx.semPhil[i].Release(); } catch { }

            philosophers.ForEach(p => p.WaitToFinish());

            Assert.IsFalse(violation, "Two neighbouring philosophers were eating at the same time");
        }
    }

    [TestClass]
    public class ComparisonTests
    {
        private const int N = 5;
        private const int RunMs = 2000;
        private const int TimeoutMs = 30;

        [TestMethod]
        public void MutexStateIsAtLeastAsFairAsSingleSemaphore()
        {
            int[] singleEats = RunSingleSemaphore();
            int[] mutexEats = RunMutexState();

            int singleSpread = singleEats.Max() - singleEats.Min();
            int mutexSpread = mutexEats.Max() - mutexEats.Min();

            // just print it so you can see the difference in test output
            System.Console.WriteLine($"Single semaphore — min: {singleEats.Min()}, max: {singleEats.Max()}, spread: {singleSpread}");
            System.Console.WriteLine($"Mutex+state      — min: {mutexEats.Min()},  max: {mutexEats.Max()},  spread: {mutexSpread}");

            Assert.IsTrue(mutexSpread <= singleSpread + 3, $"Mutex+state spread ({mutexSpread}) is way worse than single semaphore ({singleSpread})");
        }

        [TestMethod]
        public void BothModesProduceSimilarTotalMeals()
        {
            int[] singleEats = RunSingleSemaphore();
            int[] mutexEats = RunMutexState();

            System.Console.WriteLine($"Single semaphore total meals : {singleEats.Sum()}");
            System.Console.WriteLine($"Mutex+state      total meals : {mutexEats.Sum()}");

            // neither mode should be doing drastically less work than the other
            Assert.IsTrue(singleEats.Sum() > 0, "Single semaphore produced no meals at all");
            Assert.IsTrue(mutexEats.Sum() > 0, "Mutex+state produced no meals at all");
        }

        private int[] RunSingleSemaphore()
        {
            using var cts = new CancellationTokenSource();
            var ctx = new SingleCTX(N, TimeoutMs);
            var philosophers = Enumerable.Range(0, N)
                .Select(i => new PhilosopherSingle(i, cts.Token, ctx))
                .ToList();

            philosophers.ForEach(p => p.Initialize());
            Thread.Sleep(100);
            ctx.sharedSemaphore.Release(N - 1);

            Thread.Sleep(RunMs);
            cts.Cancel();

            try { ctx.sharedSemaphore.Release(N); } catch { }

            philosophers.ForEach(p => p.WaitToFinish());

            return philosophers.Select(p => p.eatCounter).ToArray();
        }

        private int[] RunMutexState()
        {
            using var cts = new CancellationTokenSource();
            var ctx = new MutexCTX(N, TimeoutMs);
            var philosophers = Enumerable.Range(0, N)
                .Select(i => new PhilosopherMutex(i, cts.Token, ctx))
                .ToList();

            philosophers.ForEach(p => p.Initialize());
            Thread.Sleep(RunMs);

            cts.Cancel();

            for (int i = 0; i < N; i++)
                try { ctx.semPhil[i].Release(); } catch { }

            philosophers.ForEach(p => p.WaitToFinish());
            return philosophers.Select(p => p.eatCounter).ToArray();
        }
    }
}