
import sqlite3
import json
import re
from datetime import timedelta
from collections import defaultdict

db_path = '/Users/levente/AI/codeMRI/data/sqlite/benchmarks.db'

def parse_duration(dur_str):
    # Format: "00:00:04.3311153" or "HH:MM:SS.microseconds"
    # Python datetime.strptime is picky about microseconds length (wants 6 digits usually).
    # We can split by : and .
    try:
        if not dur_str: return 0.0
        parts = dur_str.split(':')
        hours = int(parts[0])
        minutes = int(parts[1])
        seconds_part = parts[2]
        
        if '.' in seconds_part:
            sec_split = seconds_part.split('.')
            seconds = int(sec_split[0])
            microseconds_str = sec_split[1]
            # Truncate to 6 digits for microseconds if longer, or pad
            microseconds = int(microseconds_str[:6].ljust(6, '0'))
        else:
            seconds = int(seconds_part)
            microseconds = 0
            
        td = timedelta(hours=hours, minutes=minutes, seconds=seconds, microseconds=microseconds)
        return td.total_seconds()
    except Exception as e:
        return 0.0

def analyze_benchmarks():
    conn = sqlite3.connect(db_path)
    cursor = conn.cursor()
    
    # We only care about entries that have a valid QualityScore (meaning evaluation finished)
    # But wait, looking at the logs, some models might not have finished evaluation but we still want their speed.
    # The logs showed scores for Mistral but not others.
    # Let's see what the DB has. If QualityScore is NULL or 0, we still want performance stats.
    
    query = "SELECT QualityScore, JsonContent FROM PageBenchmarks"
    cursor.execute(query)
    rows = cursor.fetchall()
    
    stats = defaultdict(lambda: {'durations': [], 'scores': [], 'throughputs': []})
    
    for quality_score, json_str in rows:
        try:
            data = json.loads(json_str)
            gen_metrics = data.get('GenerationMetrics', {})
            model = gen_metrics.get('ModelName', 'Unknown')
            
            # Duration
            dur_str = gen_metrics.get('Duration')
            duration_sec = parse_duration(dur_str)
            
            # Throughput
            throughput = gen_metrics.get('TokensPerSecond', 0.0)
            
            # Quality Score
            # If explicit column is null/zero, check JSON or skip score aggregation
            # Ensure quality_score is float
            score = float(quality_score) if quality_score is not None else 0.0
            
            if model and model != 'Unknown':
                if duration_sec > 0:
                    stats[model]['durations'].append(duration_sec)
                    stats[model]['throughputs'].append(throughput)
                
                # Only count score if > 0 (assuming 0 means not evaluated yet)
                if score > 0:
                    stats[model]['scores'].append(score)
                    
        except Exception as e:
            continue
            
    conn.close()
    
    # Print Table
    print(f"{'Model':<30} | {'Count':<5} | {'Avg Latency (s)':<15} | {'Avg Throughput (tok/s)':<22} | {'Avg Score':<10}")
    print("-" * 100)
    
    # Sort by Avg Score descending, then Throughput
    results = []
    
    for model, data in stats.items():
        durations = data['durations']
        scores = data['scores']
        throughputs = data['throughputs']
        
        count = len(durations)
        if count == 0: continue
        
        avg_lat = sum(durations) / count
        avg_tps = sum(throughputs) / count
        avg_score = sum(scores) / len(scores) if scores else 0.0
        
        results.append((model, count, avg_lat, avg_tps, avg_score))

    # Sort logic: primary Score, secondary Throughput
    results.sort(key=lambda x: (x[4], x[3]), reverse=True)
    
    for r in results:
        print(f"{r[0]:<30} | {r[1]:<5} | {r[2]:<15.2f} | {r[3]:<22.2f} | {r[4]:<10.2f}")

    print("-" * 100)
    
    # Best Identification
    if not results:
        print("No data found.")
        return

    best_score_model = results[0]
    fastest_model = max(results, key=lambda x: x[3])
    
    print(f"\nHighest Quality:    {best_score_model[0]} (Score: {best_score_model[4]:.2f})")
    print(f"Fastest Throughput: {fastest_model[0]} ({fastest_model[3]:.2f} tok/s)")

if __name__ == "__main__":
    analyze_benchmarks()
