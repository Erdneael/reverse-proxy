#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["reverse-proxy/ReverseProxyApp/ReverseProxyApp.csproj", "reverse-proxy/ReverseProxyApp/"]
RUN dotnet restore "reverse-proxy/ReverseProxy/ReverseProxy.csproj"
COPY . .
WORKDIR "/src/reverse-proxy/ReverseProxyApp"
RUN dotnet build "ReverseProxyApp.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "ReverseProxyApp.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ReverseProxyApp.dll"]