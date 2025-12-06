FROM node:20-slim

WORKDIR /app

# Install build dependencies and curl for healthcheck
RUN apt-get update && apt-get install -y curl python3 make g++ git clang && rm -rf /var/lib/apt/lists/*

# Copy package files first for better caching
COPY package*.json ./

# Install dependencies and rebuild tree-sitter from source
RUN npm ci --legacy-peer-deps && \
    npm rebuild tree-sitter --build-from-source && \
    npm rebuild tree-sitter-c --build-from-source && \
    npm rebuild tree-sitter-c-sharp --build-from-source && \
    npm rebuild tree-sitter-cpp --build-from-source && \
    npm rebuild tree-sitter-java --build-from-source && \
    npm rebuild tree-sitter-javascript --build-from-source && \
    npm rebuild tree-sitter-python --build-from-source && \
    npm rebuild tree-sitter-typescript --build-from-source


# Copy source code  
COPY . .

# Expose port
EXPOSE 3000

# Run the service using the full (non-mock) version
CMD ["npm", "run", "start:full"]
