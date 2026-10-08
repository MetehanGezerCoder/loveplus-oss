# Love+ API container.
#
# Build from the repository root:
#   docker build -t loveplus-api:0.5.0 .
#
# The image contains no secrets. Connection strings, the JWT signing key, the demo password
# and Firebase credentials are supplied at run time through environment variables or mounted
# files, and the API refuses to start in Production when any of them is missing or is still
# an example placeholder.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

COPY Directory.Build.props LovePlus.slnx ./
COPY src/LovePlus.Domain/LovePlus.Domain.csproj src/LovePlus.Domain/
COPY src/LovePlus.Application/LovePlus.Application.csproj src/LovePlus.Application/
COPY src/LovePlus.Infrastructure/LovePlus.Infrastructure.csproj src/LovePlus.Infrastructure/
COPY src/LovePlus.Api/LovePlus.Api.csproj src/LovePlus.Api/
RUN dotnet restore src/LovePlus.Api/LovePlus.Api.csproj

COPY src/ src/
RUN dotnet publish src/LovePlus.Api/LovePlus.Api.csproj \
    -c Release \
    -o /app \
    --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# TLS terminates at the reverse proxy in front of this container, so Kestrel serves plain
# HTTP on the internal network only. Api__RequireHttps keeps the API rejecting any request
# the proxy reports as cleartext.
ENV ASPNETCORE_URLS=http://0.0.0.0:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_NOLOGO=1 \
    DOTNET_CLI_TELEMETRY_OPTOUT=1

RUN apt-get update \
    && apt-get install --no-install-recommends --yes curl \
    && rm -rf /var/lib/apt/lists/* \
    && useradd --uid 10001 --create-home --shell /usr/sbin/nologin loveplus
COPY --from=build /app ./
USER 10001

EXPOSE 8080
# /health/ready reports the PostgreSQL and Redis adapters; /health/live only reports that the
# process is accepting requests, which is what an orchestrator restart decision should use.
HEALTHCHECK --interval=15s --timeout=5s --start-period=40s --retries=5 \
    CMD curl --fail --silent http://127.0.0.1:8080/health/live || exit 1

ENTRYPOINT ["dotnet", "LovePlus.Api.dll"]
