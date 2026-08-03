FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY src/shared/AFIE.Contracts/AFIE.Contracts.csproj shared/AFIE.Contracts/
COPY src/api/feature-engineering/AFIE.FeatureEngineering.csproj api/feature-engineering/
RUN dotnet restore api/feature-engineering/AFIE.FeatureEngineering.csproj
COPY src/shared/AFIE.Contracts/ shared/AFIE.Contracts/
COPY src/api/feature-engineering/ api/feature-engineering/
RUN dotnet publish api/feature-engineering/AFIE.FeatureEngineering.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS runtime
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "AFIE.FeatureEngineering.dll"]