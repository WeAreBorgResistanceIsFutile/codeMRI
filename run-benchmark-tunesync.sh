#!/bin/bash
set -e

echo "Starting benchmark sequence for TuneSync..."

echo "[1/18] Running 1.1 - Tunsync"
dotnet run --project codeMRI.Benchmark/codeMRI.Benchmark.csproj --input "https://github.com/WilliamNT/tunesynctool.git" --config ".benchmarks/config.json" --name "Run 1.1 - Tunsync"

echo "[2/18] Running 2.1 - Tunsync"
dotnet run --project codeMRI.Benchmark/codeMRI.Benchmark.csproj --input "https://github.com/WilliamNT/tunesynctool.git" --config ".benchmarks/config2.json" --name "Run 2.1 - Tunsync"

echo "[3/18] Running 3.1 - Tunsync"
dotnet run --project codeMRI.Benchmark/codeMRI.Benchmark.csproj --input "https://github.com/WilliamNT/tunesynctool.git" --config ".benchmarks/config3.json" --name "Run 3.1 - Tunsync"

echo "[4/18] Running 4.1 - Tunsync"
dotnet run --project codeMRI.Benchmark/codeMRI.Benchmark.csproj --input "https://github.com/WilliamNT/tunesynctool.git" --config ".benchmarks/config4.json" --name "Run 4.1 - Tunsync"

echo "[5/18] Running 5.1 - Tunsync"
dotnet run --project codeMRI.Benchmark/codeMRI.Benchmark.csproj --input "https://github.com/WilliamNT/tunesynctool.git" --config ".benchmarks/config5.json" --name "Run 5.1 - Tunsync"

echo "[6/18] Running 6.1 - Tunsync"
dotnet run --project codeMRI.Benchmark/codeMRI.Benchmark.csproj --input "https://github.com/WilliamNT/tunesynctool.git" --config ".benchmarks/config6.json" --name "Run 6.1 - Tunsync"

echo "[7/18] Running 1.2 - Tunsync"
dotnet run --project codeMRI.Benchmark/codeMRI.Benchmark.csproj --input "https://github.com/WilliamNT/tunesynctool.git" --config ".benchmarks/config.json" --name "Run 1.2 - Tunsync"

echo "[8/18] Running 2.2 - Tunsync"
dotnet run --project codeMRI.Benchmark/codeMRI.Benchmark.csproj --input "https://github.com/WilliamNT/tunesynctool.git" --config ".benchmarks/config2.json" --name "Run 2.2 - Tunsync"

echo "[9/18] Running 3.2 - Tunsync"
dotnet run --project codeMRI.Benchmark/codeMRI.Benchmark.csproj --input "https://github.com/WilliamNT/tunesynctool.git" --config ".benchmarks/config3.json" --name "Run 3.2 - Tunsync"

echo "[10/18] Running 4.2 - Tunsync"
dotnet run --project codeMRI.Benchmark/codeMRI.Benchmark.csproj --input "https://github.com/WilliamNT/tunesynctool.git" --config ".benchmarks/config4.json" --name "Run 4.2 - Tunsync"

echo "[11/18] Running 5.2 - Tunsync"
dotnet run --project codeMRI.Benchmark/codeMRI.Benchmark.csproj --input "https://github.com/WilliamNT/tunesynctool.git" --config ".benchmarks/config5.json" --name "Run 5.2 - Tunsync"

echo "[12/18] Running 6.2 - Tunsync"
dotnet run --project codeMRI.Benchmark/codeMRI.Benchmark.csproj --input "https://github.com/WilliamNT/tunesynctool.git" --config ".benchmarks/config6.json" --name "Run 6.2 - Tunsync"

echo "[13/18] Running 1.3 - Tunsync"
dotnet run --project codeMRI.Benchmark/codeMRI.Benchmark.csproj --input "https://github.com/WilliamNT/tunesynctool.git" --config ".benchmarks/config.json" --name "Run 1.3 - Tunsync"

echo "[14/18] Running 2.3 - Tunsync"
dotnet run --project codeMRI.Benchmark/codeMRI.Benchmark.csproj --input "https://github.com/WilliamNT/tunesynctool.git" --config ".benchmarks/config2.json" --name "Run 2.3 - Tunsync"

echo "[15/18] Running 3.3 - Tunsync"
dotnet run --project codeMRI.Benchmark/codeMRI.Benchmark.csproj --input "https://github.com/WilliamNT/tunesynctool.git" --config ".benchmarks/config3.json" --name "Run 3.3 - Tunsync"

echo "[16/18] Running 4.3 - Tunsync"
dotnet run --project codeMRI.Benchmark/codeMRI.Benchmark.csproj --input "https://github.com/WilliamNT/tunesynctool.git" --config ".benchmarks/config4.json" --name "Run 4.3 - Tunsync"

echo "[17/18] Running 5.3 - Tunsync"
dotnet run --project codeMRI.Benchmark/codeMRI.Benchmark.csproj --input "https://github.com/WilliamNT/tunesynctool.git" --config ".benchmarks/config5.json" --name "Run 5.3 - Tunsync"

echo "[18/18] Running 6.3 - Tunsync"
dotnet run --project codeMRI.Benchmark/codeMRI.Benchmark.csproj --input "https://github.com/WilliamNT/tunesynctool.git" --config ".benchmarks/config6.json" --name "Run 6.3 - Tunsync"

echo "Benchmark sequence complete."
