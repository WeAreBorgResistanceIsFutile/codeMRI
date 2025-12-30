
import csv
import sys
from collections import defaultdict

def analyze_csv(file_path):
    stats = defaultdict(lambda: {'durations': [], 'chars': []})
    current_model = None
    
    with open(file_path, 'r') as f:
        reader = csv.DictReader(f)
        for row in reader:
            if row['type'] == 'token_count' and row['model']:
                current_model = row['model']
            
            if row['type'] == 'response' and row['duration_ms'] and current_model:
                try:
                    duration = float(row['duration_ms'])
                    chars = int(row['length_chars']) if row['length_chars'] else 0
                    stats[current_model]['durations'].append(duration)
                    stats[current_model]['chars'].append(chars)
                except ValueError:
                    pass

    print(f"{'Model':<30} | {'Count':<5} | {'Avg Latency (ms)':<18} | {'Throughput (chars/s)':<22}")
    print("-" * 85)
    
    best_latency_model = None
    min_latency = float('inf')
    
    best_throughput_model = None
    max_throughput = 0.0

    for model, data in stats.items():
        durations = data['durations']
        chars = data['chars']
        if not durations:
            continue
        
        avg_dur = sum(durations) / len(durations)
        total_dur = sum(durations)
        total_chars = sum(chars)
        
        # Throughput = Total Chars / (Total Duration in seconds)
        # Duration is in ms, so divide by 1000
        throughput = total_chars / (total_dur / 1000.0) if total_dur > 0 else 0
        
        count = len(durations)
        
        print(f"{model:<30} | {count:<5} | {avg_dur:<18.2f} | {throughput:<22.2f}")
        
        if avg_dur < min_latency:
            min_latency = avg_dur
            best_latency_model = model
            
        if throughput > max_throughput:
            max_throughput = throughput
            best_throughput_model = model

    print("-" * 85)
    print(f"Fastest Latency:    {best_latency_model} ({min_latency:.2f} ms)")
    print(f"Highest Throughput: {best_throughput_model} ({max_throughput:.2f} chars/s)")
    
    # Conclusion
    if best_latency_model == best_throughput_model:
        print(f"Overall Best:       {best_latency_model}")
    else:
        print(f"Mixed Results: Fast latency -> {best_latency_model}, High throughput -> {best_throughput_model}")

if __name__ == "__main__":
    analyze_csv('/Users/levente/AI/codeMRI/benchmark_perf.csv')
