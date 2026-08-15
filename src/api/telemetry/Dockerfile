FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY src/shared/AFIE.Contracts/AFIE.Contracts.csproj shared/AFIE.Contracts/
COPY src/api/telemetry/AFIE.Telemetry.csproj api/telemetry/
RUN dotnet restore api/telemetry/AFIE.Telemetry.csproj
COPY src/shared/AFIE.Contracts/ shared/AFIE.Contracts/
COPY src/api/telemetry/ api/telemetry/
RUN dotnet publish api/telemetry/AFIE.Telemetry.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS runtime
WORKDIR /app
COPY --from=build /app .
RUN mkdir -p /app/data
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "AFIE.Telemetry.dll"]
