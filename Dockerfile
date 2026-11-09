# Production image: builds the frontend and the backend in separate stages and ships only the runtime.
# Lives at the repository root because it builds both frontend/ and backend/.
#
#   docker build -t financial-monitor .
#   docker run -p 8080:8080 financial-monitor

# --- Stage 1: frontend (static files) ----------------------------------------------------------------
FROM node:24-alpine AS frontend
WORKDIR /src

# Dependencies first, so this layer is cached until package*.json changes.
COPY frontend/package.json frontend/package-lock.json ./
RUN npm ci

COPY frontend/ ./
RUN npm run build

# --- Stage 2: backend (publish) ----------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS backend
WORKDIR /src

# Restore first, so this layer is cached until the project file changes.
COPY backend/src/FinancialMonitor.Api/FinancialMonitor.Api.csproj FinancialMonitor.Api/
RUN dotnet restore FinancialMonitor.Api/FinancialMonitor.Api.csproj

COPY backend/src/FinancialMonitor.Api/ FinancialMonitor.Api/
RUN dotnet publish FinancialMonitor.Api/FinancialMonitor.Api.csproj \
    --configuration Release --no-restore --output /app /p:UseAppHost=false

# --- Stage 3: runtime ----------------------------------------------------------------------------------
# ASP.NET runtime only (no SDK, no Node), on Alpine for a small image.
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS runtime
WORKDIR /app

COPY --from=backend /app ./
# The API serves the built frontend from wwwroot, so UI and API share one origin.
COPY --from=frontend /src/dist ./wwwroot

# Non-root: the .NET images ship an unprivileged "app" user; 8080 needs no root privileges.
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID

ENTRYPOINT ["dotnet", "FinancialMonitor.Api.dll"]
