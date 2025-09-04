# https://github.com/Azure/azure-cli/issues/19591
# https://iceburn.medium.com/azure-cli-docker-containers-7059750be1f2
FROM mcr.microsoft.com/dotnet/runtime-deps:9.0-alpine AS base
ENV DOTNET_CLI_TELEMETRY_OPTOUT=true \
    AZ_INSTALLER=DOCKER
RUN apk add --no-cache py3-pip && \
    apk add --no-cache --virtual=build gcc musl-dev python3-dev libffi-dev openssl-dev cargo make && \
    pip install --no-cache-dir --break-system-packages azure-cli && \
    apk del --purge build
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV AZURE_CONFIG_DIR=/app/.azure


FROM mcr.microsoft.com/dotnet/sdk:9.0-alpine AS publish
WORKDIR /src
COPY . .
RUN dotnet publish "AzureCliCredentialProxy.csproj" -c Release -r linux-musl-x64 -o /app/publish


FROM base AS final
RUN chown -R app /app
USER app
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["./AzureCliCredentialProxy"]
