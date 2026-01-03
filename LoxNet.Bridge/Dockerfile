# build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY LoxNet.Client ./LoxNet.Client
COPY LoxNet.Bridge ./LoxNet.Bridge
RUN dotnet restore LoxNet.Bridge/src/Bridge/Bridge.csproj
RUN dotnet publish LoxNet.Bridge/src/Bridge/Bridge.csproj -c Release -o /app/publish

# runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish ./
EXPOSE 8080
ENTRYPOINT ["dotnet", "Bridge.dll"]
