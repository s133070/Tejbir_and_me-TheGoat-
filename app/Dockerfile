FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Unbreakable.csproj packages.lock.json* ./
RUN dotnet restore
COPY . ./
RUN dotnet publish Unbreakable.csproj -c Release -o /out --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
# Beveiligingsupdates van Ubuntu installeren (o.a. apt); de basis-image loopt soms achter. Dit was een fout dat trivy vond recente CVE --> CVE-2026-109518
RUN apt-get update && apt-get upgrade -y && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /out ./
EXPOSE 8080
ENTRYPOINT ["dotnet", "Unbreakable.dll"]
