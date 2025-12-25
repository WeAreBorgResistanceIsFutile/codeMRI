#!/bin/bash

echo "🛑 Stopping all containers..."
docker compose down

echo "🔨 Building AST service image..."
docker build -t codemri-ast-service:latest ./codeMRI.ASTService

echo "🔨 Building MCP server image..."
docker build -t codemri-mcp-server:latest -f codeMRI.MCP/Dockerfile .

echo "🚀 Starting all containers..."
docker compose up -d

echo "✅ Done! Containers are running."
echo "📊 Check container status: docker compose ps"
echo "📋 View logs: docker compose logs -f"
echo "🔍 View MCP logs: docker compose logs -f mcp-server"