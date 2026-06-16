FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore — separate layer so it's cached unless .csproj changes
COPY ["Planora.Api/Planora.Api.csproj", "Planora.Api/"]
COPY ["Planora.Shared/Planora.Shared.csproj", "Planora.Shared/"]
RUN dotnet restore "Planora.Api/Planora.Api.csproj"

# Build & publish
COPY . .
WORKDIR "/src/Planora.Api"
RUN dotnet publish "Planora.Api.csproj" -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "Planora.Api.dll"]
