using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Environments;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Diagnostics.Tracing.Parsers.AspNet;
using System;
using System.Collections;
using System.Linq;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;

namespace AlgBenchmark
{
    public class Program
    {

        [MinColumn, MaxColumn, MeanColumn, MedianColumn]
        [MemoryDiagnoser(false)]
        public class FindNotExistingNumbers
        {

            public FindNotExistingNumbers()
            {
            }
            //, 
            [Params(1_000_000, 10_000_000, 100_000_000)]//, 10_000_000, 100_000_000, 1_000_000_000)]
            public int N;

            [Params(5, 10, 100, 250)]
            public int K;

            public int DataSize;

            public int[] data;
            public long[] result;
            public Random random = new Random(42);

            [GlobalSetup]
            public void Setup()
            {
                DataSize = N - K;
                result = GetNumbers().Distinct().Take(K).Order().ToArray();
                data = new int[DataSize];
                long pos = 0;
                for (int i = 0; i < N; i++)
                {
                    if (!result.Contains(i))
                        data[pos++] = i;
                }
                random.Shuffle(data);
            }

            public IEnumerable<long> GetNumbers()
            {
                while (true)
                    yield return random.NextInt64(0, N);
            }

            //[Benchmark]
            public long[] SimpleCounter()
            {
                return SimpleCounterImpl();
            }

            public long[] SimpleCounterImpl()
            {
                if (N * K > 100_000)
                    return this.result;
                List<long> result = new List<long>();
                for (int i = 0; i < N; i++)
                {
                    bool numberExist = false;
                    for (int j = 0; j < DataSize; j++)
                    {
                        if (data[j] == i)
                        {
                            numberExist = true;
                            break;
                        }
                    }
                    if (!numberExist)
                        result.Add(i);
                }
                return result.Order().ToArray();
            }

            //[Benchmark]
            public long[] AdvanceCounterWithCache()
            {
                if (N * K > 10_000_000)
                    return this.result;
                int P = N / 25;
                List<long> result = new List<long>();
                HashSet<long> seen = new HashSet<long>(P);
                for (int i = 0; i < N; i++)
                {
                    if (seen.Contains(i))
                    {
                        seen.Remove(i);
                        continue;
                    }
                    bool numberExist = false;
                    for (int j = 0; j < DataSize; j++)
                    {
                        if (data[j] == i)
                        {
                            numberExist = true;
                            break;
                        }
                        else if (seen.Count < P && data[j] > i && data[j] <= i + P)
                            seen.Add(data[j]);
                    }
                    if (!numberExist)
                        result.Add(i);
                }
                return result.Order().ToArray();
            }

            //[Benchmark]
            public long[] HalvingDivider()
            {
                if (N * K > 100_000_000)
                    return this.result;
                List<long> result = new List<long>();

                void HalvingDividerImpl(long from, long to)
                {

                    var targetCount = to - from + 1;

                    var currentCount = 0;
                    for (int i = 0; i < DataSize; i++)
                        if (from <= data[i] && data[i] <= to)
                            currentCount++;

                    if (currentCount == targetCount)
                        return; //No diff in interval
                    /* //Only one number in interval - can use adding/XOR alg
                    else if (currentCount + 1 == targetCount) {

                    }
                    */
                    else
                    {
                        if (from == to)
                            result.Add(from);
                        else
                        {
                            var middle = (from + to) / 2;

                            HalvingDividerImpl(from, middle);
                            HalvingDividerImpl(middle + 1, to);
                        }
                    }



                }

                HalvingDividerImpl(0, DataSize);
                return result.Order().ToArray();
            }


            //[Benchmark]
            public long[] AdvancedHalvingDivider()
            {
                List<long> result = new List<long>();

                void SingleNumberFinder(long from, long to)
                {
                    long sum = 0;
                    for (long i = from; i <= to; i++)
                    {
                        sum ^= i;
                    }
                    for (long i = 0; i < DataSize; i++)
                    {
                        if (from <= data[i] && data[i] <= to)
                            sum ^= data[i];
                    }
                    result.Add(sum);
                }

                void HalvingDividerImpl(long from, long to)
                {

                    var targetCount = to - from + 1;

                    var currentCount = 0;
                    for (int i = 0; i < DataSize; i++)
                        if (from <= data[i] && data[i] <= to)
                            currentCount++;

                    if (currentCount == targetCount)
                        return; //No diff in interval
                    //Only one number in interval - can use adding/XOR alg
                    else if (currentCount + 1 == targetCount)
                    {
                        SingleNumberFinder(from, to);
                    }
                    else
                    {
                        if (from == to)
                            result.Add(from);
                        else
                        {
                            var middle = (from + to) / 2;

                            HalvingDividerImpl(from, middle);
                            HalvingDividerImpl(middle + 1, to);
                        }
                    }



                }

                HalvingDividerImpl(0, DataSize);
                return result.Order().ToArray();
            }


            //[Benchmark]
            public long[] SmartBLDividerSumFinder()
            {
                List<long> result = new List<long>();

                void SumFinder(long from, long to)
                {
                    long tsum = to * (to + 1) / 2;
                    long fsum = (from) * (from + 1) / 2;
                    long ttsum = tsum - fsum;

                    long sum = 0;
                    foreach (var d in data)
                    {
                        if (from <= d && d <= to)
                            sum += d;
                    }

                    result.Add(ttsum + from - sum);
                }

                /*
                void SingleNumberFinderBLOldVectors(long from, long to)
                {
                    long sum = 0;
                    long i = from;
                    for (; i + 7 <= to; i += 8)
                    {
                        sum ^= i ^ (i + 1) ^ (i + 2) ^ (i + 3) ^ (i + 4) ^ (i + 5) ^ (i + 6) ^ (i + 7);
                    }
                    for (; i <= to; i++)
                    {
                        sum ^= i;
                    }
                    i = 0;
                    foreach (var d in data)
                    {
                        if (from <= d && d <= to)
                            sum ^= d;
                    }
                    result.Add(sum);
                }
                */
                var dividerManyCount = 32;
                BitArray bitArray = new(dividerManyCount, false);
                void ManyNumberFind(int from, int to)
                {
                    bitArray.SetAll(false);
                    for (int i = 0; i < DataSize; i += 1)
                    {
                        if (from <= data[i + 0] && data[i + 0] <= to)
                            bitArray.Set(data[i + 0] - from, true);
                    }
                    var tt = to - from;
                    for (int i = 0; i <= tt; i += 1)
                    {
                        if (!bitArray[i])
                            result.Add(i + from);
                    }
                }


                void DividerImpl(int from, int to)
                {
                    var targetCount = to - from + 1;
                    if (targetCount <= dividerManyCount)
                    {
                        ManyNumberFind(from, to);
                        return;
                    }

                    var t2l = (from + to) / 2;
                    var t2r = t2l + 1;

                    var t1l = (from + t2l) / 2;
                    var t1r = t1l + 1;

                    var t3l = (t2r + to) / 2;
                    var t3r = t3l + 1;

                    //from - t1l, t1r - t2l, t2r - t3l, t3r - to

                    long c1 = 0, c2 = 0, c3 = 0, c4 = 0;

                    var ct1 = ftCount(from, t1l);
                    var ct2 = ftCount(t1r, t2l);
                    var ct3 = ftCount(t2r, t3l);
                    var ct4 = ftCount(t3r, to);


                    foreach (var d in data)
                    {
                        if (from <= d && d <= t1l)
                            c1 += 1;
                        else if (t1r <= d && d <= t2l)
                            c2 += 1;
                        else if (t2r <= d && d <= t3l)
                            c3 += 1;
                        else if (t3r <= d && d <= to)
                            c4 += 1;
                    }

                    if (ct1 == 1)
                        result.Add(from);
                    else if (c1 + 1 == ct1)
                        SumFinder(from, t1l);
                    else if (c1 < ct1)
                        DividerImpl(from, t1l);

                    if (ct2 == 1)
                        result.Add(t1r);
                    else if (c2 + 1 == ct2)
                        SumFinder(t1r, t2l);
                    else if (c2 < ct2)
                        DividerImpl(t1r, t2l);

                    if (ct2 == 1)
                        result.Add(t2r);
                    else if (c3 + 1 == ct3)
                        SumFinder(t2r, t3l);
                    else if (c3 < ct3)
                        DividerImpl(t2r, t3l);

                    if (ct3 == 1)
                        result.Add(t3r);
                    else if (c4 + 1 == ct4)
                        SumFinder(t3r, to);
                    else if (c4 < ct4)
                        DividerImpl(t3r, to);
                }

                DividerImpl(0, DataSize);
                return result.Order().ToArray();
            }

            [Benchmark]
            public long[] SmartBLDividerSumFinderXUltra()
            {
                List<long> result = new List<long>();

                void SumFinder(long from, long to)
                {
                    long tsum = to * (to + 1) / 2;
                    long fsum = (from) * (from + 1) / 2;
                    long ttsum = tsum - fsum;

                    long sum = 0;
                    foreach (var d in data)
                    {
                        if (from <= d && d <= to)
                            sum += d;
                    }

                    result.Add(ttsum + from - sum);
                }

                var dividerManyCount = 1024;
                BitArray bitArray = new(dividerManyCount, false);
                void ManyNumberFind(int from, int to)
                {
                    bitArray.SetAll(false);
                    foreach (var d in data)
                    {
                        if (from <= d && d <= to)
                            bitArray.Set(d - from, true);
                    }
                    var tt = to - from;
                    for (int i = 0; i <= tt; i += 1)
                    {
                        if (!bitArray[i])
                            result.Add(i + from);
                    }
                }

                void DividerImpl(int from, int to)
                {
                    var targetCount = to - from + 1;

                    if (targetCount <= dividerManyCount)
                    {
                        ManyNumberFind(from, to);
                        return;
                    }


                    var k = 128;
                    int[] c = new int[k];
                    int cSize = targetCount / k + 1;

                    foreach (var d in data)
                    {
                        if (from <= d && d <= to)
                        {
                            int index = (int)((d - from) / cSize);
                            c[index]++;
                        }
                    }

                    for (int i = 0; i < k; i++)
                    {
                        int cfrom = i * cSize;
                        int cto = Math.Min(cfrom + cSize - 1, targetCount - 1);
                        cfrom += from;
                        cto += from;
                        long target = Math.Min(cSize, targetCount - i * cSize);

                        if (target == 1)
                            result.Add(from);
                        else if (c[i] + 1 == target)
                            SumFinder(cfrom, cto);
                        else if (c[i] < target)
                            DividerImpl(cfrom, cto);
                    }
                }

                DividerImpl(0, DataSize);
                return result.Order().ToArray();
            }

            [Benchmark(Baseline = true)]
            public long[] SmartBLDividerSumFinderImpossible()
            {
                List<long> result = new List<long>();

                void SumFinder(long from, long to)
                {
                    long tsum = to * (to + 1) / 2;
                    long fsum = (from) * (from + 1) / 2;
                    long ttsum = tsum - fsum;

                    long sum = 0;
                    foreach (var d in data)
                    {
                        if (from <= d && d <= to)
                            sum += d;
                    }

                    result.Add(ttsum + from - sum);
                }

                var dividerManyCount = 1024;
                BitArray bitArray = new(dividerManyCount, false);
                void ManyNumberFind(int from, int to)
                {
                    bitArray.SetAll(false);
                    foreach (var d in data)
                    {
                        if (from <= d && d <= to)
                            bitArray.Set(d - from, true);
                    }
                    var tt = to - from;
                    for (int i = 0; i <= tt; i += 1)
                    {
                        if (!bitArray[i])
                            result.Add(i + from);
                    }
                }

                void DividerImpl(int from, int to)
                {
                    var targetCount = to - from + 1;

                    if (targetCount <= dividerManyCount)
                    {
                        ManyNumberFind(from, to);
                        return;
                    }


                    var k = K + K / 2;
                    int[] c = new int[k];
                    int cSize = targetCount / k + 1;

                    foreach (var d in data)
                    {
                        if (from <= d && d <= to)
                        {
                            int index = (int)((d - from) / cSize);
                            c[index]++;
                        }
                    }

                    for (int i = 0; i < k; i++)
                    {
                        int cfrom = i * cSize;
                        int cto = Math.Min(cfrom + cSize - 1, targetCount - 1);
                        cfrom += from;
                        cto += from;
                        long target = Math.Min(cSize, targetCount - i * cSize);

                        if (target == 1)
                            result.Add(from);
                        else if (c[i] + 1 == target)
                            SumFinder(cfrom, cto);
                        else if (c[i] < target)
                            DividerImpl(cfrom, cto);
                    }
                }

                int bucketCount = 1024;
                int bucketSize = N / bucketCount + 1;

                int[] bucketCounts = new int[bucketCount];

                foreach (long num in this.data)
                {
                    int bucketIndex = (int)(num / bucketSize);
                    bucketCounts[bucketIndex]++;
                }

                List<int> bucketsWithMissingNumbers = new List<int>();
                for (int i = 0; i < bucketCount; i++)
                {
                    long expectedCount = Math.Min(bucketSize, N - i * bucketSize);
                    if (bucketCounts[i] < expectedCount)
                    {
                        if (bucketCounts[i] + 1 == expectedCount)
                        {
                            bucketsWithMissingNumbers.Add(i);
                        }
                        else
                        {
                            int startNum = i * bucketSize;
                            int endNum = Math.Min(startNum + bucketSize - 1, N - 1);
                            SumFinder(startNum, endNum);
                        }
                    }
                }

                foreach (int bucketIndex in bucketsWithMissingNumbers)
                {
                    int startNum = bucketIndex * bucketSize;
                    int endNum = Math.Min(startNum + bucketSize - 1, N - 1);
                    DividerImpl(startNum, endNum);
                }

                return result.Order().ToArray();
            }


            //[Benchmark]
            public long[] GPT1OPreviewSolutionOriginal()
            {

                // Генерируем массив с пропущенными числами

                // Параметры бакетов
                int bucketCount = 10_000; // Количество бакетов
                long bucketSize = N / bucketCount + 1;

                // Шаг 1: Инициализируем массив счетчиков
                long[] bucketCounts = new long[bucketCount];

                // Первый проход: Подсчет чисел в каждом бакете
                foreach (long num in this.data)
                {
                    int bucketIndex = (int)(num / bucketSize);
                    bucketCounts[bucketIndex]++;
                }

                // Шаг 2: Определяем бакеты с пропущенными числами
                List<int> bucketsWithMissingNumbers = new List<int>();
                for (int i = 0; i < bucketCount; i++)
                {
                    long expectedCount = Math.Min(bucketSize, N - i * bucketSize);
                    if (bucketCounts[i] < expectedCount)
                    {
                        bucketsWithMissingNumbers.Add(i);
                    }
                }

                // Шаг 3: Ищем пропущенные числа в проблемных бакетах
                List<long> missingNumbers = new List<long>();

                foreach (int bucketIndex in bucketsWithMissingNumbers)
                {
                    long startNum = bucketIndex * bucketSize;
                    long endNum = Math.Min(startNum + bucketSize - 1, N - 1);
                    long bucketRangeSize = endNum - startNum + 1;

                    // Инициализируем битовый массив для бакета
                    int bitArraySize = (int)bucketRangeSize;
                    BitArray bitArray = new BitArray(bitArraySize);

                    // Второй проход: Заполняем битовый массив
                    foreach (long num in this.data)
                    {
                        if (num >= startNum && num <= endNum)
                        {
                            int index = (int)(num - startNum);
                            bitArray.Set(index, true);
                        }
                    }

                    // Находим пропущенные числа в бакете
                    for (int i = 0; i < bitArraySize; i++)
                    {
                        if (!bitArray.Get(i))
                        {
                            long missingNum = startNum + i;
                            missingNumbers.Add(missingNum);
                        }
                    }
                }

                return missingNumbers.Order().ToArray();
            }

            //[Benchmark]
            public long[] BucketsPlusSmartBLOldVectors()
            {

                // Генерируем массив с пропущенными числами

                // Параметры бакетов
                int bucketCount = 10_000; // Количество бакетов
                long bucketSize = N / bucketCount + 1;

                // Шаг 1: Инициализируем массив счетчиков
                int[] bucketCounts = new int[bucketCount];

                // Первый проход: Подсчет чисел в каждом бакете
                foreach (long num in this.data)
                {
                    int bucketIndex = (int)(num / bucketSize);
                    bucketCounts[bucketIndex]++;
                }

                List<long> missingNumbers = new List<long>();
                void SingleNumberFinderBLOldVectors(long from, long to)
                {
                    long sum = 0;
                    long i = from;
                    for (; i + 7 <= to; i += 8)
                    {
                        sum ^= i ^ (i + 1) ^ (i + 2) ^ (i + 3) ^ (i + 4) ^ (i + 5) ^ (i + 6) ^ (i + 7);
                    }
                    for (; i <= to; i++)
                    {
                        sum ^= i;
                    }
                    i = 0;
                    foreach (var d in data)
                    {
                        if (from <= d && d <= to)
                            sum ^= d;
                    }
                    missingNumbers.Add(sum);
                }

                // Шаг 2: Определяем бакеты с пропущенными числами
                List<int> bucketsWithMissingNumbers = new List<int>();
                for (int i = 0; i < bucketCount; i++)
                {
                    long expectedCount = Math.Min(bucketSize, N - i * bucketSize);
                    if (bucketCounts[i] < expectedCount)
                    {
                        if (bucketCounts[i] + 1 == expectedCount)
                        {
                            long startNum = i * bucketSize;
                            long endNum = Math.Min(startNum + bucketSize - 1, N - 1);
                            SingleNumberFinderBLOldVectors(startNum, endNum);
                        }
                        else
                        {
                            bucketsWithMissingNumbers.Add(i);
                        }
                    }
                }

                // Шаг 3: Ищем пропущенные числа в проблемных бакетах

                BitArray bitArray = new BitArray((int)bucketSize);
                foreach (int bucketIndex in bucketsWithMissingNumbers)
                {
                    long startNum = bucketIndex * bucketSize;
                    long endNum = Math.Min(startNum + bucketSize - 1, N - 1);
                    long bucketRangeSize = endNum - startNum + 1;

                    // Инициализируем битовый массив для бакета
                    int bitArraySize = (int)bucketRangeSize;
                    bitArray.SetAll(false);

                    // Второй проход: Заполняем битовый массив
                    foreach (long num in this.data)
                    {
                        if (num >= startNum && num <= endNum)
                        {
                            int index = (int)(num - startNum);
                            bitArray.Set(index, true);
                        }
                    }

                    // Находим пропущенные числа в бакете
                    for (int i = 0; i < bitArraySize; i++)
                    {
                        if (!bitArray.Get(i))
                        {
                            long missingNum = startNum + i;
                            missingNumbers.Add(missingNum);
                        }
                    }
                }

                return missingNumbers.Order().ToArray();
            }


            [Benchmark]
            public long[] BucketsPlusSumFinder()
            {

                // Генерируем массив с пропущенными числами

                int bucketCount = 1_000; // Количество бакетов
                long bucketSize = N / bucketCount + 1;

                int[] bucketCounts = new int[bucketCount];

                foreach (long num in this.data)
                {
                    int bucketIndex = (int)(num / bucketSize);
                    bucketCounts[bucketIndex]++;
                }

                List<long> missingNumbers = new List<long>();
                void SumFinder(long from, long to)
                {

                    long tsum = to * (to + 1) / 2;
                    long fsum = (from) * (from + 1) / 2;
                    long ttsum = tsum - fsum;

                    long sum = 0;
                    foreach (var d in data)
                    {
                        if (from <= d && d <= to)
                            sum += d;
                    }
                    missingNumbers.Add(ttsum + from - sum);
                }

                List<int> bucketsWithMissingNumbers = new List<int>();
                for (int i = 0; i < bucketCount; i++)
                {
                    long expectedCount = Math.Min(bucketSize, N - i * bucketSize);
                    if (bucketCounts[i] < expectedCount)
                    {
                        if (bucketCounts[i] + 1 == expectedCount)
                        {
                            long startNum = i * bucketSize;
                            long endNum = Math.Min(startNum + bucketSize - 1, N - 1);
                            SumFinder(startNum, endNum);
                        }
                        else
                        {
                            bucketsWithMissingNumbers.Add(i);
                        }
                    }
                }


                BitArray bitArray = new BitArray((int)bucketSize);
                foreach (int bucketIndex in bucketsWithMissingNumbers)
                {
                    long startNum = bucketIndex * bucketSize;
                    long endNum = Math.Min(startNum + bucketSize - 1, N - 1);
                    long bucketRangeSize = endNum - startNum + 1;

                    int bitArraySize = (int)bucketRangeSize;
                    bitArray.SetAll(false);

                    foreach (long num in this.data)
                    {
                        if (num >= startNum && num <= endNum)
                        {
                            int index = (int)(num - startNum);
                            bitArray.Set(index, true);
                        }
                    }

                    for (int i = 0; i < bitArraySize; i++)
                    {
                        if (!bitArray.Get(i))
                        {
                            long missingNum = startNum + i;
                            missingNumbers.Add(missingNum);
                        }
                    }
                }

                return missingNumbers.Order().ToArray();
            }


            static long ftCount(long left, long right) => right - left + 1;
        }


        public static void Main(string[] args)
        {
            var config = DefaultConfig.Instance
                .WithSummaryStyle(SummaryStyle.Default.WithTimeUnit(Perfolizer.Horology.TimeUnit.Millisecond))
                .AddJob(Job.Default
                    .WithAffinity(64)
                    .WithStrategy(RunStrategy.ColdStart)
                    .WithIterationCount(5)
                    .WithWarmupCount(2)
                    .WithLaunchCount(1)
                );
            //[MinColumn, MaxColumn, MeanColumn, MedianColumn]

            /*
            var d = new FindNotExistingNumbers()
            {
                N = 100_000
            };
            d.Setup();
            //var a1 = d.SimpleCounter();
            //var a2 = d.AdvanceCounterWithCache();
            //var a3 = d.HalvingDivider();
            var a4 = d.AdvancedHalvingDivider();
            var a5 = d.SmartBLDividerSumFinder();
            var a6 = d.GPT1OPreviewSolutionOriginal();
            var a7 = d.GPT1OPreviewPlusSmartBLOldVectors();
            var a8 = d.GPT1OPreviewPlusSumFinder();
            var a9 = d.SmartBLDividerSumFinderX16();
            var a10 = d.SmartBLDividerSumFinderXUltra();
            */




            var summary = BenchmarkRunner.Run<FindNotExistingNumbers>(config);
        }
    }
}
