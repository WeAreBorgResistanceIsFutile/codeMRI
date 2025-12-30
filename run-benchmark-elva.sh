#!/bin/bash
set -e

echo "Starting benchmark sequence for Elva..."

echo "[1/18] Running 1.1 - Elva"
dotnet run --project codeMRI.Benchmark/codeMRI.Benchmark.csproj --input "https://github.com/WilliamNT/Elva.git" --config ".benchmarks/config.json" --name "Run 1.7 - Elva"

echo "[2/18] Running 2.1 - Elva"
dotnet run --project codeMRI.Benchmark/codeMRI.Benchmark.csproj --input "https://github.com/WilliamNT/Elva.git" --config ".benchmarks/config2.json" --name "Run 2.7 - Elva"

echo "[3/18] Running 3.1 - Elva"
dotnet run --project codeMRI.Benchmark/codeMRI.Benchmark.csproj --input "https://github.com/WilliamNT/Elva.git" --config ".benchmarks/config3.json" --name "Run 3.7 - Elva"

echo "[4/18] Running 4.1 - Elva"
dotnet run --project codeMRI.Benchmark/codeMRI.Benchmark.csproj --input "https://github.com/WilliamNT/Elva.git" --config ".benchmarks/config4.json" --name "Run 4.7 - Elva"

echo "[5/18] Running 5.1 - Elva"
dotnet run --project codeMRI.Benchmark/codeMRI.Benchmark.csproj --input "https://github.com/WilliamNT/Elva.git" --config ".benchmarks/config5.json" --name "Run 5.7 - Elva"

echo "[6/18] Running 6.1 - Elva"
dotnet run --project codeMRI.Benchmark/codeMRI.Benchmark.csproj --input "https://github.com/WilliamNT/Elva.git" --config ".benchmarks/config6.json" --name "Run 6.7 - Elva"

echo "[7/18] Running 1.2 - Elva"
dotnet run --project codeMRI.Benchmark/codeMRI.Benchmark.csproj --input "https://github.com/WilliamNT/Elva.git" --config ".benchmarks/config.json" --name "Run 1.8 - Elva"

echo "[8/18] Running 2.2 - Elva"
dotnet run --project codeMRI.Benchmark/codeMRI.Benchmark.csproj --input "https://github.com/WilliamNT/Elva.git" --config ".benchmarks/config2.json" --name "Run 2.8 - Elva"

echo "[9/18] Running 3.2 - Elva"
dotnet run --project codeMRI.Benchmark/codeMRI.Benchmark.csproj --input "https://github.com/WilliamNT/Elva.git" --config ".benchmarks/config3.json" --name "Run 3.8 - Elva"

echo "[10/18] Running 4.2 - Elva"
dotnet run --project codeMRI.Benchmark/codeMRI.Benchmark.csproj --input "https://github.com/WilliamNT/Elva.git" --config ".benchmarks/config4.json" --name "Run 4.8 - Elva"

echo "[11/18] Running 5.2 - Elva"
dotnet run --project codeMRI.Benchmark/codeMRI.Benchmark.csproj --input "https://github.com/WilliamNT/Elva.git" --config ".benchmarks/config5.json" --name "Run 5.8 - Elva"

echo "[12/18] Running 6.2 - Elva"
dotnet run --project codeMRI.Benchmark/codeMRI.Benchmark.csproj --input "https://github.com/WilliamNT/Elva.git" --config ".benchmarks/config6.json" --name "Run 6.8 - Elva"

echo "[13/18] Running 1.3 - Elva"
dotnet run --project codeMRI.Benchmark/codeMRI.Benchmark.csproj --input "https://github.com/WilliamNT/Elva.git" --config ".benchmarks/config.json" --name "Run 1.9 - Elva"

echo "[14/18] Running 2.3 - Elva"
dotnet run --project codeMRI.Benchmark/codeMRI.Benchmark.csproj --input "https://github.com/WilliamNT/Elva.git" --config ".benchmarks/config2.json" --name "Run 2.9 - Elva"

echo "[15/18] Running 3.3 - Elva"
dotnet run --project codeMRI.Benchmark/codeMRI.Benchmark.csproj --input "https://github.com/WilliamNT/Elva.git" --config ".benchmarks/config3.json" --name "Run 3.9 - Elva"

echo "[16/18] Running 4.3 - Elva"
dotnet run --project codeMRI.Benchmark/codeMRI.Benchmark.csproj --input "https://github.com/WilliamNT/Elva.git" --config ".benchmarks/config4.json" --name "Run 4.9 - Elva"

echo "[17/18] Running 5.3 - Elva"
dotnet run --project codeMRI.Benchmark/codeMRI.Benchmark.csproj --input "https://github.com/WilliamNT/Elva.git" --config ".benchmarks/config5.json" --name "Run 5.9 - Elva"

echo "[18/18] Running 6.3 - Elva"
dotnet run --project codeMRI.Benchmark/codeMRI.Benchmark.csproj --input "https://github.com/WilliamNT/Elva.git" --config ".benchmarks/config6.json" --name "Run 6.9 - Elva"

echo "Benchmark sequence complete."
