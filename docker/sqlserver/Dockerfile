FROM mcr.microsoft.com/mssql/server:2022-latest

USER root

RUN set -eux; \
    apt-get update; \
    apt-get install -y --no-install-recommends \
    curl \
    apt-transport-https \
    gnupg \
    ca-certificates; \
    if [ ! -f /etc/apt/sources.list.d/mssql-server-2022.list ]; then \
    curl -sSL https://packages.microsoft.com/keys/microsoft.asc | gpg --dearmor > /etc/apt/trusted.gpg.d/microsoft.gpg; \
    curl -sSL https://packages.microsoft.com/config/ubuntu/22.04/mssql-server-2022.list > /etc/apt/sources.list.d/mssql-server-2022.list; \
    fi; \
    apt-get update; \
    ACCEPT_EULA=Y apt-get install -y --no-install-recommends mssql-server-fts; \
    apt-get clean; \
    rm -rf /var/lib/apt/lists/*

USER mssql
