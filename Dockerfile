# See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

# This stage is used when running from VS in fast mode (Default for Debug configuration)
FROM mcr.microsoft.com/dotnet/runtime:8.0 AS base
USER app
WORKDIR /app


# This stage is used to build the service project
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["nuget.config", "."]
COPY ["ToaArchiver.Docker/ToaArchiver.Docker.csproj", "ToaArchiver.Docker/"]
COPY ["ToaArchiver.Archives/ToaArchiver.Archives.csproj", "ToaArchiver.Archives/"]
COPY ["ToaArchiver.Domain/ToaArchiver.Domain.csproj", "ToaArchiver.Domain/"]
COPY ["ToaArchiver.Worker/ToaArchiver.Worker.csproj", "ToaArchiver.Worker/"]
# Restore dependencies
ARG PAT
RUN dotnet nuget remove source SDO --configfile nuget.config
RUN dotnet nuget add source https://pkgs.dev.azure.com/USN-DUIT/_packaging/SDO/nuget/v3/index.json --configfile nuget.config --name SDO --username az --password ${PAT} --store-password-in-clear-text
RUN dotnet restore "./ToaArchiver.Docker/ToaArchiver.Docker.csproj"
COPY . .
WORKDIR "/src/ToaArchiver.Docker"
RUN dotnet build "./ToaArchiver.Docker.csproj" -c $BUILD_CONFIGURATION -o /app/build

# This stage is used to publish the service project to be copied to the final stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./ToaArchiver.Docker.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# This stage is used in production or when running from VS in regular mode (Default when not using the Debug configuration)
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ToaArchiver.Docker.dll"]