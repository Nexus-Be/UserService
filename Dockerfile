FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8001
ENV ASPNETCORE_URLS=http://+:8001

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["User.sln", "."]
COPY ["User.Api/User.Api.csproj", "User.Api/"]
COPY ["User.Application/User.Application.csproj", "User.Application/"]
COPY ["User.Domain/User.Domain.csproj", "User.Domain/"]
COPY ["User.Infrastructure/User.Infrastructure.csproj", "User.Infrastructure/"]
COPY ["User.Tests/User.Tests.csproj", "User.Tests/"]
RUN dotnet restore "User.sln"
COPY . .
RUN dotnet build "User.Api/User.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "User.Api/User.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "User.Api.dll"]
