#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/runtime:6.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["nuget.config", "."]
COPY ["ToaArchiver.Docker/ToaArchiver.Docker.csproj", "ToaArchiver.Docker/"]
RUN dotnet restore "ToaArchiver.Docker/ToaArchiver.Docker.csproj"
COPY . .
WORKDIR "/src/ToaArchiver.Docker"
RUN dotnet build "ToaArchiver.Docker.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "ToaArchiver.Docker.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ToaArchiver.Docker.dll"]