#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/core/runtime:3.0-buster-slim AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/core/sdk:3.0-buster AS build
WORKDIR /src
COPY ["TestRtspClient/TestRtspClient.csproj", "TestRtspClient/"]
COPY ["FrameDecoderCore/FrameDecoderCore.csproj", "FrameDecoderCore/"]
COPY ["RtspClientSharpCore/RtspClientSharpCore.csproj", "RtspClientSharpCore/"]
RUN dotnet restore "TestRtspClient/TestRtspClient.csproj"
COPY . .
WORKDIR "/src/TestRtspClient"
RUN dotnet build "TestRtspClient.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "TestRtspClient.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "TestRtspClient.dll"]