FROM node:18.20.8-slim

WORKDIR /app

# Install curl for healthcheck
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

# Copy package files first for better caching
COPY package*.json ./

# Copy source code  
COPY . .

# Install dependencies without tree-sitter for now
RUN npm install --legacy-peer-deps --ignore-optional

# Expose port
EXPOSE 3000

# Run the service using the full (non-mock) version
CMD ["npm", "run", "start:full"]
