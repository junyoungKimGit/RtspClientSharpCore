#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/core/runtime:3.0-buster-slim AS base
WORKDIR /app
RUN apt-get update && apt-get install -y libgdiplus && apt-get install -y ffmpeg
ENV LD_LIBRARY_PATH /app:$LD_LIBRARY_PATH

FROM mcr.microsoft.com/dotnet/core/sdk:3.0-buster AS build
WORKDIR /src
COPY ["TestRtspClient/TestRtspClient.csproj", "TestRtspClient/"]
COPY ["FrameDecoderCore/FrameDecoderCore.csproj", "FrameDecoderCore/"]
COPY ["RtspClientSharpCore/RtspClientSharpCore.csproj", "RtspClientSharpCore/"]
RUN dotnet restore "TestRtspClient/TestRtspClient.csproj"
COPY TestRtspClient/lib/x64/libffmpeghelper.so /src
COPY . .
WORKDIR "/src/TestRtspClient"
RUN dotnet build "TestRtspClient.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "TestRtspClient.csproj" -c Release -o /app/publish
COPY TestRtspClient/lib/x64/libffmpeghelper.so /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
COPY TestRtspClient/lib/x64/libffmpeghelper.so /app
EXPOSE 7000
ENTRYPOINT ["dotnet", "TestRtspClient.dll"]