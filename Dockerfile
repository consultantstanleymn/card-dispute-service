FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1
ENV DOTNET_NOLOGO=1
WORKDIR /src
COPY . .
RUN dotnet restore src/CardDisputes.Api/CardDisputes.Api.csproj
RUN dotnet publish src/CardDisputes.Api/CardDisputes.Api.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
USER root
RUN apt-get update \
 && apt-get install -y --only-upgrade apt libapt-pkg6.0t64 \
 && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app/publish .
USER app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "CardDisputes.Api.dll"]
