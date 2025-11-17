# Multi-stage Dockerfile for F# Counter App
# Stage 1: Build frontend
FROM node:20-alpine AS frontend-build

WORKDIR /app

# Copy package files
COPY package.json package-lock.json* ./

# Install npm dependencies
RUN npm install

# Copy source files
COPY src/Client ./src/Client
COPY src/Shared ./src/Shared
COPY vite.config.js tailwind.config.js ./

# Install .NET SDK for Fable compilation
RUN apk add --no-cache dotnet8-sdk

# Restore .NET dependencies
COPY src/Client/Client.fsproj ./src/Client/
COPY src/Shared/Shared.fsproj ./src/Shared/
RUN dotnet restore src/Client/Client.fsproj

# Build frontend
RUN npm run build

# Stage 2: Build backend
FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS backend-build

WORKDIR /app

# Copy project files
COPY src/Shared/Shared.fsproj ./src/Shared/
COPY src/Server/Server.fsproj ./src/Server/

# Restore dependencies
RUN dotnet restore src/Server/Server.fsproj

# Copy source code
COPY src/Shared ./src/Shared
COPY src/Server ./src/Server

# Build backend
RUN dotnet publish src/Server/Server.fsproj -c Release -o /app/publish

# Stage 3: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS runtime

WORKDIR /app

# Install curl for healthchecks
RUN apk add --no-cache curl

# Copy backend from build stage
COPY --from=backend-build /app/publish .

# Copy frontend from build stage
COPY --from=frontend-build /app/dist/public ./dist/public

# Create data directory for persistence
RUN mkdir -p /app/data

# Expose port
EXPOSE 5000

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
  CMD curl -f http://localhost:5000/ || exit 1

# Run the application
ENTRYPOINT ["dotnet", "Server.dll"]
