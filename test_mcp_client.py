#!/usr/bin/env python3
import subprocess
import json
import sys
import time

# Configuration
MCP_SERVER_DLL = "/Users/levente/AI/codeMRI/codeMRI.MCP/bin/Debug/net10.0/codeMRI.MCP.dll"
REPO_PATH = "/Users/levente/AI/codeMRI"
DOTNET_CMD = "dotnet"

def create_message(method, params=None, msg_id=1):
    msg = {
        "jsonrpc": "2.0",
        "id": msg_id,
        "method": method
    }
    if params:
        msg["params"] = params
    return msg

def run_test():
    print(f"🚀 Starting MCP Server from: {MCP_SERVER_DLL}")
    
    # Start the MCP server process
    process = subprocess.Popen(
        [DOTNET_CMD, MCP_SERVER_DLL, "--repository", REPO_PATH, "--update-strategy", "hybrid"],
        stdin=subprocess.PIPE,
        stdout=subprocess.PIPE,
        stderr=sys.stderr,
        text=True,
        bufsize=1
    )

    try:
        # 1. Initialize
        print("\n[Test 1] Sending 'initialize'...")
        init_msg = create_message("initialize", {
            "protocolVersion": "2024-11-05",
            "capabilities": {},
            "clientInfo": {"name": "TestClient", "version": "1.0"}
        }, 1)
        
        process.stdin.write(json.dumps(init_msg) + "\n")
        process.stdin.flush()

        response = process.stdout.readline()
        print(f"✅ Init Response received ({len(response)} chars)")

        # Send 'notifications/initialized'
        process.stdin.write(json.dumps({
            "jsonrpc": "2.0",
            "method": "notifications/initialized"
        }) + "\n")
        process.stdin.flush()

        print("⏳ Waiting 10 seconds for initial indexing...")
        time.sleep(10)

        # 2. List Tools
        print("\n[Test 2] Sending 'tools/list'...")
        list_msg = create_message("tools/list", {}, 2)
        process.stdin.write(json.dumps(list_msg) + "\n")
        process.stdin.flush()
        
        response = process.stdout.readline()
        print(f"Raw response: {response[:200]}...")
        
        tools_data = json.loads(response)
        print(f"Parsed JSON keys: {tools_data.keys()}")
        
        # The response format is: {"id": 2, "result": {"tools": [...]}}
        result = tools_data.get('result', {})
        print(f"Result keys: {result.keys()}")
        
        tools = result.get('tools', [])
        tool_names = [t['name'] for t in tools]
        print(f"✅ Found {len(tool_names)} tools: {', '.join(tool_names)}")

        # 3. Test 'find_references'
        print("\n[Test 3] Testing 'find_references' for 'GraphIndexService'...")
        call_msg = create_message("tools/call", {
            "name": "find_references",
            "arguments": {
                "symbolName": "GraphIndexService"
            }
        }, 3)
        process.stdin.write(json.dumps(call_msg) + "\n")
        process.stdin.flush()

        response = process.stdout.readline()
        if response:
            res_json = json.loads(response)
            print(f"Response keys: {res_json.keys()}")
            
            # Check for error first
            if 'error' in res_json:
                print(f"❌ Error: {res_json['error']}")
            else:
                result = res_json.get('result', {})
                print(f"Result keys: {result.keys()}")
                
                # MCP tools/call response format: {"content": [{"type": "text", "text": "..."}]}
                content_items = result.get('content', [])
                if content_items:
                    text = content_items[0].get('text', '')
                    print(f"✅ Result:\n{text[:500]}...")
                else:
                    print(f"⚠️  No content in result")
        else:
            print("❌ No response received")

    except Exception as e:
        import traceback
        print(f"❌ Error: {e}")
        traceback.print_exc()
    finally:
        print("\n🛑 Closing server...")
        process.terminate()

if __name__ == "__main__":
    run_test()
