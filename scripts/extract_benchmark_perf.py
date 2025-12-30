#!/usr/bin/env python3
import re
import sys
import argparse
import csv
from datetime import datetime

def parse_log_file(filepath):
    events = []
    
    # Regex patterns
    # 2025-12-30 12:10:08.540 +01:00 [INF] LLM responded successfully (response length: 979 chars, 4553ms)
    re_response = re.compile(r'LLM responded successfully \(response length: (\d+) chars, (\d+)ms\)')
    
    # 2025-12-30 12:10:08.552 +01:00 [INF] LLMServiceFacade executing request (system: (\d+) chars, text: (\d+) chars, history: (\d+) messages\)
    re_request = re.compile(r'LLMServiceFacade executing request \(system: (\d+) chars, text: (\d+) chars')
    
    # 2025-12-30 12:10:08.552 +01:00 [INF] Sending ChatAsync request to /api/chat with \d+ messages \((\d+) tokens\)
    re_tokens = re.compile(r'Sending ChatAsync request .* \((\d+) tokens\)')

    re_timestamp = re.compile(r'^(\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3})')

    with open(filepath, 'r', encoding='utf-8', errors='ignore') as f:
        for line in f:
            ts_match = re_timestamp.match(line)
            timestamp = ts_match.group(1) if ts_match else None
            
            resp_match = re_response.search(line)
            if resp_match:
                events.append({
                    'timestamp': timestamp,
                    'type': 'response',
                    'length_chars': int(resp_match.group(1)),
                    'duration_ms': int(resp_match.group(2))
                })
                continue
                
            req_match = re_request.search(line)
            if req_match:
                events.append({
                    'timestamp': timestamp,
                    'type': 'request_start',
                    'system_chars': int(req_match.group(1)),
                    'text_chars': int(req_match.group(2))
                })
                continue

            tok_match = re_tokens.search(line)
            if tok_match:
                event = {
                    'timestamp': timestamp,
                    'type': 'token_count',
                    'tokens': int(tok_match.group(1))
                }
                # Try to extract model if present in Body
                model_match = re.search(r'"model":"([^"]+)"', line)
                if model_match:
                    event['model'] = model_match.group(1)
                
                events.append(event)
    
    return events

def analyze_performance(events):
    responses = [e for e in events if e['type'] == 'response']
    requests = [e for e in events if e['type'] == 'request_start']
    token_counts = [e for e in events if e['type'] == 'token_count']
    
    total_requests = len(responses)
    total_duration_ms = sum(r['duration_ms'] for r in responses)
    total_output_chars = sum(r['length_chars'] for r in responses)
    avg_duration = total_duration_ms / total_requests if total_requests > 0 else 0
    
    total_input_chars = sum(r['text_chars'] + r.get('system_chars', 0) for r in requests)
    total_tokens = sum(t['tokens'] for t in token_counts)
    
    return {
        'total_requests': total_requests,
        'total_duration_ms': total_duration_ms,
        'avg_duration_ms': avg_duration,
        'total_output_chars': total_output_chars,
        'total_input_chars': total_input_chars,
        'total_tokens': total_tokens,
        'start_time': events[0]['timestamp'] if events else None,
        'end_time': events[-1]['timestamp'] if events else None
    }

def main():
    parser = argparse.ArgumentParser(description='Extract performance info from benchmark logs.')
    parser.add_argument('logfile', help='Path to the log file')
    parser.add_argument('--csv', action='store_true', help='Output details to CSV')
    args = parser.parse_args()
    
    events = parse_log_file(args.logfile)
    stats = analyze_performance(events)
    
    print("\nBenchmark Performance Summary")
    print("==========================")
    print(f"Total Requests:       {stats['total_requests']}")
    print(f"Total Duration (LLM): {stats['total_duration_ms']} ms")
    print(f"Avg Duration:         {stats['avg_duration_ms']:.2f} ms")
    print(f"Total Output Chars:   {stats['total_output_chars']}")
    print(f"Total Input Chars:    {stats['total_input_chars']}")
    if stats['total_tokens'] > 0:
        print(f"Total Tokens:         {stats['total_tokens']}")
    
    if args.csv:
        csv_filename = 'benchmark_perf.csv'
        print(f"\nWriting detailed log to {csv_filename}...")
        with open(csv_filename, 'w', newline='') as f:
            writer = csv.DictWriter(f, fieldnames=['timestamp', 'type', 'duration_ms', 'length_chars', 'system_chars', 'text_chars', 'tokens', 'model'])
            writer.writeheader()
            for event in events:
                writer.writerow(event)
        print("Done.")

if __name__ == "__main__":
    main()
