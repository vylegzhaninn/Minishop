FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY Directory.Build.props ./
COPY proto/ proto/
COPY src/Analytics/ src/Analytics/
RUN dotnet publish src/Analytics/Analytics.Worker/Analytics.Worker.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8082
EXPOSE 8082
ENTRYPOINT ["dotnet", "Analytics.Worker.dll"]
