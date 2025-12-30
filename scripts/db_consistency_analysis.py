
import sqlite3
import json
from datetime import timedelta
from collections import defaultdict
import math

DB_PATH = '/Users/levente/AI/codeMRI/data/sqlite/benchmarks.db'

def parse_duration(dur_str: str) -> float:
    """Convert a duration string like '00:00:04.3311153' to seconds (float)."""
    if not dur_str:
        return 0.0
    try:
        parts = dur_str.split(':')
        hours = int(parts[0])
        minutes = int(parts[1])
        sec_part = parts[2]
        if '.' in sec_part:
            sec, micro = sec_part.split('.')
            seconds = int(sec)
            # Pad/truncate microseconds to 6 digits for datetime compatibility
            micro = (micro + '0'*6)[:6]
            microseconds = int(micro)
        else:
            seconds = int(sec_part)
            microseconds = 0
        td = timedelta(hours=hours, minutes=minutes, seconds=seconds, microseconds=microseconds)
        return td.total_seconds()
    except Exception:
        return 0.0

def analyze_consistency():
    conn = sqlite3.connect(DB_PATH)
    cur = conn.cursor()
    cur.execute("SELECT QualityScore, JsonContent FROM PageBenchmarks")
    rows = cur.fetchall()
    conn.close()

    # Data structures per model
    data = defaultdict(lambda: {
        'scores': [],
        'latencies': [],
        'throughputs': []
    })

    for quality_score, json_str in rows:
        try:
            payload = json.loads(json_str)
            gen = payload.get('GenerationMetrics', {})
            model = gen.get('ModelName', 'Unknown')
            # Duration (latency)
            duration = parse_duration(gen.get('Duration'))
            # Throughput (tokens per second)
            tps = float(gen.get('TokensPerSecond', 0.0))
            # Quality score – use column if >0, otherwise fallback to OverallQualityScore in JSON
            score = float(quality_score) if quality_score not in (None, 0) else float(payload.get('OverallQualityScore', 0))
            if model == 'Unknown':
                continue
            if duration:
                data[model]['latencies'].append(duration)
            if tps:
                data[model]['throughputs'].append(tps)
            if score > 0:
                data[model]['scores'].append(score)
        except Exception:
            continue

    # Compute consistency (standard deviation) for each metric
    def stddev(lst):
        if len(lst) < 2:
            return 0.0
        mean = sum(lst) / len(lst)
        var = sum((x - mean) ** 2 for x in lst) / (len(lst) - 1)
        return math.sqrt(var)

    print(f"{'Model':<30} | {'Count':<5} | {'Total Time (s)':<15} | {'Avg Score':<10} | {'Score σ':<8} | {'Latency σ (s)':<14} | {'TPS σ':<8}")
    print('-' * 100)
    best_consistency = None
    best_score_consistency = None
    for model, vals in data.items():
        cnt = len(vals['scores']) or len(vals['latencies']) or len(vals['throughputs'])
        total_time = sum(vals['latencies'])
        avg_score = sum(vals['scores']) / len(vals['scores']) if vals['scores'] else 0.0
        score_sd = stddev(vals['scores'])
        latency_sd = stddev(vals['latencies'])
        tps_sd = stddev(vals['throughputs'])
        print(f"{model:<30} | {cnt:<5} | {total_time:<15.2f} | {avg_score:<10.4f} | {score_sd:<8.4f} | {latency_sd:<14.4f} | {tps_sd:<8.4f}")
        # Choose the model with the smallest score standard deviation (most consistent quality)
        if vals['scores']:
            if best_score_consistency is None or score_sd < best_score_consistency[1]:
                best_score_consistency = (model, score_sd)
        # If we want overall consistency, we could combine metrics, but quality is the primary KPI.

    if best_score_consistency:
        print('\nMost consistent quality (lowest σ):', best_score_consistency[0],
              f"(σ={best_score_consistency[1]:.4f})")
    else:
        print('\nNo quality scores available to assess consistency.')

if __name__ == '__main__':
    analyze_consistency()
