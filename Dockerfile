FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["ECommerceSite.csproj", "."]
RUN dotnet restore "ECommerceSite.csproj"
COPY . .
RUN dotnet build "ECommerceSite.csproj" -c Release -o /app/build --no-restore

FROM build AS publish
RUN dotnet publish "ECommerceSite.csproj" -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ECommerceSite.dll"]
