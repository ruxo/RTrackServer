# https://hub.docker.com/_/microsoft-dotnet
FROM mcr.microsoft.com/dotnet/sdk:5.0 AS build
WORKDIR /source

# copy csproj and restore as distinct layers
COPY *.sln .
COPY ./RTrack.Common/*.csproj ./RTrack.Common/
COPY ./RTrackClient/*.csproj ./RTrackClient/
COPY ./RTrackServer/*.csproj ./RTrackServer/
RUN dotnet restore

# copy everything else and build app
COPY ./RTrack.Common/. ./RTrack.Common/
COPY ./RTrackClient/. ./RTrackClient/
COPY ./RTrackServer/. ./RTrackServer/
WORKDIR /source/RTrackServer
RUN dotnet publish -c release -o /app --no-restore

# final stage/image
FROM mcr.microsoft.com/dotnet/aspnet:5.0 AS server
WORKDIR /app
COPY --from=build /app ./
ENTRYPOINT ["dotnet", "RTrackServer.dll"]