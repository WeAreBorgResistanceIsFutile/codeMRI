
import unittest
import tempfile
import os
import csv
from extract_benchmark_perf import parse_log_file, analyze_performance

class TestBenchmarkExtraction(unittest.TestCase):
    def setUp(self):
        self.test_log_content = """
2025-12-30 12:10:08.540 +01:00 [INF] LLM responded successfully (response length: 979 chars, 4553ms) [ at :]
2025-12-30 12:10:08.552 +01:00 [INF] LLMServiceFacade executing request (system: 45 chars, text: 4162 chars, history: 0 messages) [ at :]
2025-12-30 12:10:08.552 +01:00 [INF] Sending ChatAsync request to /api/chat with 2 messages (1051 tokens). Body: {"model":"mistral-large-3:675b-cloud","messages":[{"role":"system","content":"You are a documentation evaluation assistant."}]} [ at :]
2025-12-30 12:10:42.852 +01:00 [INF] LLM responded successfully (response length: 13958 chars, 34299ms) [ at :]
2025-12-30 12:10:42.908 +01:00 [INF] LLMServiceFacade executing request (system: 0 chars, text: 4333 chars, history: 0 messages) [ at :]
"""
        self.temp_log = tempfile.NamedTemporaryFile(delete=False, mode='w')
        self.temp_log.write(self.test_log_content)
        self.temp_log.close()

    def tearDown(self):
        os.unlink(self.temp_log.name)

    def test_parse_log_file(self):
        """Test that log lines are parsed correctly into events."""
        events = parse_log_file(self.temp_log.name)
        
        # We expect 2 completions and 2 requests (and one token count line which might be merged or separate)
        # Actually, let's see how we design the parser.
        # Ideally we extracting specifically "LLM interactions".
        
        self.assertEqual(len(events), 5, "Should identify 5 events (2 resp, 2 req, 1 token)")
        # Wait, the structure in the log is somewhat interleaved or sequential.
        # Let's verify we captured specific values.
        
        responses = [e for e in events if e['type'] == 'response']
        self.assertEqual(len(responses), 2)
        self.assertEqual(responses[0]['duration_ms'], 4553)
        self.assertEqual(responses[0]['length_chars'], 979)
        self.assertEqual(responses[1]['duration_ms'], 34299)
        
        requests = [e for e in events if e['type'] == 'request_start']
        self.assertEqual(len(requests), 2)
        self.assertEqual(requests[0]['text_chars'], 4162)

        token_counts = [e for e in events if e['type'] == 'token_count']
        self.assertEqual(len(token_counts), 1)
        self.assertEqual(token_counts[0]['model'], 'mistral-large-3:675b-cloud')

    def test_analyze_performance(self):
        """Test aggregation of stats."""
        events = parse_log_file(self.temp_log.name)
        stats = analyze_performance(events)
        
        self.assertEqual(stats['total_requests'], 2)
        self.assertEqual(stats['total_duration_ms'], 4553 + 34299)
        self.assertAlmostEqual(stats['avg_duration_ms'], (4553 + 34299) / 2)
        self.assertEqual(stats['total_output_chars'], 979 + 13958)

if __name__ == '__main__':
    unittest.main()
