FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY Directory.Build.props ./
COPY proto/ proto/
COPY src/Shared/SharedKernel/ src/Shared/SharedKernel/
COPY src/Orders/ src/Orders/
RUN dotnet publish src/Orders/Orders.Api/Orders.Api.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080;http://+:8081
EXPOSE 8080 8081
ENTRYPOINT ["dotnet", "Orders.Api.dll"]
