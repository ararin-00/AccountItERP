FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["AccountItERP.csproj", "./"]
RUN dotnet restore "AccountItERP.csproj"

COPY . .
RUN dotnet publish "AccountItERP.csproj" \
    -c Release \
    -o /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

COPY --from=build /app/publish .

EXPOSE 8080

ENTRYPOINT ["sh", "-c", "dotnet AccountItERP.dll --urls http://0.0.0.0:${PORT:-8080}"]