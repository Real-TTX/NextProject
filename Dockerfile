# syntax=docker/dockerfile:1

# ---------------------------------------------------------------------------
# Build stage
# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG Channel=local
ARG BuildNumber=0
ARG BuildDate=
WORKDIR /src

# Restore first (better layer caching).
COPY ["global.json", "Directory.Build.props", "./"]
COPY ["src/NextProject.Core/NextProject.Core.csproj", "src/NextProject.Core/"]
COPY ["src/NextProject/NextProject.csproj", "src/NextProject/"]
RUN dotnet restore "src/NextProject/NextProject.csproj"

COPY src src
# Fall back to today's date when BuildDate is not supplied, so the version never ends in "-".
RUN BUILD_DATE="${BuildDate:-$(date -u +%Y%m%d)}" && \
    dotnet publish "src/NextProject/NextProject.csproj" \
    -c Release -o /app/publish --no-restore \
    -p:UseAppHost=false \
    -p:Channel=$Channel -p:BuildNumber=$BuildNumber -p:BuildDate=$BUILD_DATE

# ---------------------------------------------------------------------------
# Runtime stage (ASP.NET Core base image)
# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_HTTP_PORTS="" \
    NEXTPROJECT_DATA=/data \
    DOTNET_gcServer=0 \
    DOTNET_TieredPGO=1

# Configuration, the keys of the cookies and secrets, attachments, scratch space of the backup: one volume.
RUN mkdir -p /data && chown -R $APP_UID:$APP_UID /data
VOLUME ["/data"]

EXPOSE 2860

COPY --from=build /app/publish .
USER $APP_UID

# The app checks itself, so the image needs no extra tools.
HEALTHCHECK --interval=30s --timeout=8s --start-period=40s --retries=3 \
    CMD ["dotnet", "NextProject.dll", "--healthcheck"]

ENTRYPOINT ["dotnet", "NextProject.dll"]
