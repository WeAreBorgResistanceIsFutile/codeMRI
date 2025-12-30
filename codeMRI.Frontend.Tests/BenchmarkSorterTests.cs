using NUnit.Framework;
using codeMRI.Frontend.Services;
using codeMRI.Core.Models;
using System.Collections.Generic;
using System.Linq;

namespace codeMRI.Frontend.Tests
{
    [TestFixture]
    public class BenchmarkSorterTests
    {
        private List<PageBenchmark> _data;

        [SetUp]
        public void SetUp()
        {
            _data = new List<PageBenchmark>
            {
                new PageBenchmark { ModuleName = "B", OverallQualityScore = 5, WordCount = 200 },
                new PageBenchmark { ModuleName = "A", OverallQualityScore = 8, WordCount = 100 },
                new PageBenchmark { ModuleName = "C", OverallQualityScore = 3, WordCount = 300 }
            };
        }

        [Test]
        public void Sort_ByModuleName_Ascending()
        {
            // Act
            var sorted = BenchmarkSorter.Sort(_data, "ModuleName", true).ToList();

            // Assert
            Assert.That(sorted[0].ModuleName, Is.EqualTo("A"));
            Assert.That(sorted[1].ModuleName, Is.EqualTo("B"));
            Assert.That(sorted[2].ModuleName, Is.EqualTo("C"));
        }

        [Test]
        public void Sort_ByModuleName_Descending()
        {
            // Act
            var sorted = BenchmarkSorter.Sort(_data, "ModuleName", false).ToList();

            // Assert
            Assert.That(sorted[0].ModuleName, Is.EqualTo("C"));
            Assert.That(sorted[1].ModuleName, Is.EqualTo("B"));
            Assert.That(sorted[2].ModuleName, Is.EqualTo("A"));
        }

        [Test]
        public void Sort_ByQualityScore_Ascending()
        {
            // Act
            var sorted = BenchmarkSorter.Sort(_data, "QualityScore", true).ToList();

            // Assert
            Assert.That(sorted[0].OverallQualityScore, Is.EqualTo(3));
            Assert.That(sorted[1].OverallQualityScore, Is.EqualTo(5));
            Assert.That(sorted[2].OverallQualityScore, Is.EqualTo(8));
        }

        [Test]
        public void Sort_ByWordCount_Descending()
        {
            // Act
            var sorted = BenchmarkSorter.Sort(_data, "WordCount", false).ToList();

            // Assert
            Assert.That(sorted[0].WordCount, Is.EqualTo(300));
            Assert.That(sorted[1].WordCount, Is.EqualTo(200));
            Assert.That(sorted[2].WordCount, Is.EqualTo(100));
        }

        [Test]
        public void Sort_ByTokens_Ascending()
        {
            var dataWithTokens = new List<PageBenchmark>
            {
                new PageBenchmark { GenerationMetrics = new BenchmarkMetrics { InputTokens = 10, OutputTokens = 20 } },
                new PageBenchmark { GenerationMetrics = new BenchmarkMetrics { InputTokens = 5, OutputTokens = 5 } },
                new PageBenchmark { GenerationMetrics = new BenchmarkMetrics { InputTokens = 50, OutputTokens = 50 } }
            };

            // Act
            var sorted = BenchmarkSorter.Sort(dataWithTokens, "Tokens", true).ToList();

            // Assert
            Assert.That(sorted[0].GenerationMetrics.TotalTokens, Is.EqualTo(10));
            Assert.That(sorted[1].GenerationMetrics.TotalTokens, Is.EqualTo(30));
            Assert.That(sorted[2].GenerationMetrics.TotalTokens, Is.EqualTo(100));
        }

        [Test]
        public void Sort_ByLatency_Descending()
        {
            var dataWithLatency = new List<PageBenchmark>
            {
                new PageBenchmark { GenerationMetrics = new BenchmarkMetrics { Latency = TimeSpan.FromSeconds(1) } },
                new PageBenchmark { GenerationMetrics = new BenchmarkMetrics { Latency = TimeSpan.FromSeconds(5) } },
                new PageBenchmark { GenerationMetrics = new BenchmarkMetrics { Latency = TimeSpan.FromSeconds(2) } }
            };

            // Act
            var sorted = BenchmarkSorter.Sort(dataWithLatency, "Latency", false).ToList();

            // Assert
            Assert.That(sorted[0].GenerationMetrics.Latency.TotalSeconds, Is.EqualTo(5));
            Assert.That(sorted[1].GenerationMetrics.Latency.TotalSeconds, Is.EqualTo(2));
            Assert.That(sorted[2].GenerationMetrics.Latency.TotalSeconds, Is.EqualTo(1));
        }
        [Test]
        public void SortRuns_ByName_Ascending()
        {
            var runs = new List<BenchmarkRun>
            {
                new BenchmarkRun { Name = "B" },
                new BenchmarkRun { Name = "A" },
                new BenchmarkRun { Name = "C" }
            };

            // Act
            var sorted = BenchmarkSorter.SortRuns(runs, "Name", true).ToList();

            // Assert
            Assert.That(sorted[0].Name, Is.EqualTo("A"));
            Assert.That(sorted[1].Name, Is.EqualTo("B"));
            Assert.That(sorted[2].Name, Is.EqualTo("C"));
        }

        [Test]
        public void SortRuns_ByQuality_Descending()
        {
            var runs = new List<BenchmarkRun>
            {
                new BenchmarkRun { MeanQualityScore = 0.5 },
                new BenchmarkRun { MeanQualityScore = 0.8 },
                new BenchmarkRun { MeanQualityScore = 0.3 }
            };

            // Act
            var sorted = BenchmarkSorter.SortRuns(runs, "Quality", false).ToList();

            // Assert
            Assert.That(sorted[0].MeanQualityScore, Is.EqualTo(0.8));
            Assert.That(sorted[1].MeanQualityScore, Is.EqualTo(0.5));
            Assert.That(sorted[2].MeanQualityScore, Is.EqualTo(0.3));
        }

        [Test]
        public void SortRuns_ByStartTime_Descending()
        {
            var now = DateTime.UtcNow;
            var runs = new List<BenchmarkRun>
            {
                new BenchmarkRun { StartTime = now.AddHours(-1) },
                new BenchmarkRun { StartTime = now },
                new BenchmarkRun { StartTime = now.AddHours(-2) }
            };

            // Act
            var sorted = BenchmarkSorter.SortRuns(runs, "StartTime", false).ToList();

            // Assert
            Assert.That(sorted[0].StartTime, Is.EqualTo(now));
            Assert.That(sorted[1].StartTime, Is.EqualTo(now.AddHours(-1)));
            Assert.That(sorted[2].StartTime, Is.EqualTo(now.AddHours(-2)));
        }
    }
}
