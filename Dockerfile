# Stage 1: Build frontend
FROM node:22-alpine AS frontend
WORKDIR /app
COPY src/Clientside/package*.json ./src/Clientside/
WORKDIR /app/src/Clientside
RUN npm ci --legacy-peer-deps
COPY src/Clientside/ ./
RUN npm run build

# Stage 2: Build backend
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS backend
WORKDIR /app
COPY src/ ./src/
COPY --from=frontend /app/src/Agoda.DevExTelemetry.WebApi/wwwroot/ ./src/Agoda.DevExTelemetry.WebApi/wwwroot/
RUN dotnet restore src/Agoda.DevExTelemetry.sln
RUN dotnet publish src/Agoda.DevExTelemetry.WebApi/Agoda.DevExTelemetry.WebApi.csproj \
    -c Release -o /publish --no-restore

# Stage 3: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=backend /publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "Agoda.DevExTelemetry.WebApi.dll"]
