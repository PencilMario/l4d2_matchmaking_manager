FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src
COPY src/L4d2Matchmaking.Contracts/L4d2Matchmaking.Contracts.csproj src/L4d2Matchmaking.Contracts/
COPY src/L4d2MatchmakingCore/L4d2MatchmakingCore.csproj src/L4d2MatchmakingCore/
RUN dotnet restore src/L4d2MatchmakingCore/L4d2MatchmakingCore.csproj

COPY src/L4d2Matchmaking.Contracts/ src/L4d2Matchmaking.Contracts/
COPY src/L4d2MatchmakingCore/ src/L4d2MatchmakingCore/
RUN dotnet publish src/L4d2MatchmakingCore/L4d2MatchmakingCore.csproj -c Release --no-restore -o /out/core

FROM mcr.microsoft.com/dotnet/aspnet:10.0

WORKDIR /app
COPY --from=build /out/core/ ./
ENV ASPNETCORE_URLS=http://0.0.0.0:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "L4d2MatchmakingCore.dll"]
