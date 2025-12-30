using System;
using System.Collections.Generic;
using System.Linq;
using codeMRI.Core.Models;

namespace codeMRI.Frontend.Services
{
    public static class BenchmarkSorter
    {
        public static IEnumerable<PageBenchmark> Sort(IEnumerable<PageBenchmark> data, string columnName, bool ascending)
        {
            if (string.IsNullOrEmpty(columnName))
            {
                return data;
            }

            Func<PageBenchmark, object> keySelector = columnName switch
            {
                "ModuleName" => p => p.ModuleName,
                "QualityScore" => p => p.OverallQualityScore,
                "WordCount" => p => p.WordCount,
                "Tokens" => p => p.GenerationMetrics.TotalTokens,
                "Latency" => p => p.GenerationMetrics.Latency,
                _ => p => p.ModuleName
            };

            return ascending ? data.OrderBy(keySelector) : data.OrderByDescending(keySelector);
        }

        public static IEnumerable<BenchmarkRun> SortRuns(IEnumerable<BenchmarkRun> data, string columnName, bool ascending)
        {
            if (string.IsNullOrEmpty(columnName))
            {
                return data;
            }

            Func<BenchmarkRun, object> keySelector = columnName switch
            {
                "Name" => r => r.Name,
                "Repository" => r => r.RepositoryName,
                "Status" => r => r.Status,
                "Quality" => r => r.MeanQualityScore,
                "StartTime" => r => r.StartTime,
                _ => r => r.StartTime
            };

            return ascending ? data.OrderBy(keySelector) : data.OrderByDescending(keySelector);
        }
    }
}
