#!/bin/bash

echo "🛑 Stopping all containers..."
docker compose down

echo "🔨 Building AST service image..."
docker build -t codemri-ast-service:latest ./codeMRI.ASTService

echo "🚀 Starting all containers..."
docker compose up -d

echo "✅ Done! Containers are running."
echo "📊 Check container status: docker-compose ps"
echo "📋 View logs: docker-compose logs -f"