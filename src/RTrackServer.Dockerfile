# https://hub.docker.com/_/microsoft-dotnet
FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /source

# copy csproj and restore as distinct layers
COPY src/RTrack.Common/*.csproj src/RTrack.Common/
COPY src/RTrackServer/*.csproj src/RTrackServer/
RUN dotnet restore "src/RTrackServer/RTrackServer.csproj"

# copy everything else and build app
COPY . .
WORKDIR /source/src/RTrackServer
RUN dotnet build -c release -o /app/build

FROM build AS publish
RUN dotnet publish -c release -o /app/publish --no-restore

# final stage/image
FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS server
WORKDIR /app
COPY --from=publish /app/publish .
ENV ASPNETCORE_FORWARDEDHEADERS_ENABLED=true
ENTRYPOINT ["dotnet", "RTrackServer.dll"]