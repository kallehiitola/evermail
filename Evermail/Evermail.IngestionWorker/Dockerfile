FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["Evermail/Evermail.IngestionWorker/Evermail.IngestionWorker.csproj", "Evermail/Evermail.IngestionWorker/"]
COPY ["Evermail/Evermail.Infrastructure/Evermail.Infrastructure.csproj", "Evermail/Evermail.Infrastructure/"]
COPY ["Evermail/Evermail.Domain/Evermail.Domain.csproj", "Evermail/Evermail.Domain/"]
COPY ["Evermail/Evermail.ServiceDefaults/Evermail.ServiceDefaults.csproj", "Evermail/Evermail.ServiceDefaults/"]
RUN dotnet restore "Evermail/Evermail.IngestionWorker/Evermail.IngestionWorker.csproj"
COPY . .
WORKDIR "/src/Evermail/Evermail.IngestionWorker"
RUN dotnet publish "Evermail.IngestionWorker.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Evermail.IngestionWorker.dll"]

