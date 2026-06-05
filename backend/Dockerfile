# Stage 1: Build the application
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy csproj and restore dependencies
COPY ["src/SecureStorage.csproj", "src/"]
RUN dotnet restore "src/SecureStorage.csproj"

# Copy the rest of the source code and build the application
COPY src/ src/
RUN dotnet publish "src/SecureStorage.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 2: Production runtime environment
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

# Switch to root to install curl for health checks, then revert to the default non-root user
USER root
RUN apt-get update && \
    apt-get install -y --no-install-recommends curl && \
    rm -rf /var/lib/apt/lists/*
USER $APP_UID

# Copy the published output from the build stage
COPY --from=build /app/publish .

# Expose port 8080 (ASP.NET Core 8.0/9.0 default port for non-root containers)
EXPOSE 8080

ENTRYPOINT ["dotnet", "SecureStorage.dll"]
