# LA-180: SmartDroneDelivery.Api image. Build context: backend/ (see docker-compose.yml).
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY SmartDroneDelivery.Api/SmartDroneDelivery.Api.csproj SmartDroneDelivery.Api/
RUN dotnet restore SmartDroneDelivery.Api/SmartDroneDelivery.Api.csproj
COPY SmartDroneDelivery.Api/ SmartDroneDelivery.Api/
RUN dotnet publish SmartDroneDelivery.Api/SmartDroneDelivery.Api.csproj --configuration Release --no-restore --output /app

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .
# Plain HTTP inside the compose network; TLS belongs to whatever proxy faces the internet.
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
# Non-root user shipped with the .NET images.
USER app
ENTRYPOINT ["dotnet", "SmartDroneDelivery.Api.dll"]
