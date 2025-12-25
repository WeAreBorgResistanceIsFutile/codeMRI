# Build stage
FROM mcr.microsoft.com/dotnet/nightly/sdk:10.0-preview AS build
WORKDIR /src

# Copy project files
COPY codeMRI.Core/codeMRI.Core.csproj codeMRI.Core/
COPY codeMRI.Agents/codeMRI.Agents.csproj codeMRI.Agents/
COPY codeMRI.Visualization/codeMRI.Visualization.csproj codeMRI.Visualization/
COPY codeMRI.Infrastructure/codeMRI.Infrastructure.csproj codeMRI.Infrastructure/
COPY codeMRI.MCP/codeMRI.MCP.csproj codeMRI.MCP/

# Restore dependencies
RUN dotnet restore codeMRI.MCP/codeMRI.MCP.csproj

# Copy source code
COPY codeMRI.Core/ codeMRI.Core/
COPY codeMRI.Agents/ codeMRI.Agents/
COPY codeMRI.Visualization/ codeMRI.Visualization/
COPY codeMRI.Infrastructure/ codeMRI.Infrastructure/
COPY codeMRI.MCP/ codeMRI.MCP/

# Build and publish
WORKDIR /src/codeMRI.MCP
RUN dotnet publish -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/nightly/runtime:10.0-preview
WORKDIR /app

# Copy published application
COPY --from=build /app/publish .

# Set environment variables with defaults
ENV CODEMRI_REPO_PATH=/repository
ENV AST_SERVICE_URL=http://ast-service:3002
ENV UPDATE_STRATEGY=hybrid
ENV INDEX_ON_START=true

# Expose stdio for MCP protocol (no ports needed)
# The MCP server uses stdin/stdout for communication

# Entry point
ENTRYPOINT ["dotnet", "codeMRI.MCP.dll"]
CMD ["--repository", "/repository", "--update-strategy", "hybrid", "--ast-service-url", "http://ast-service:3002"]
