# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Сначала только файлы проекта — так слой с restore переиспользуется,
# пока не менялись зависимости.
COPY global.json ./
COPY src/PrinzipPriceChecker.Api/PrinzipPriceChecker.Api.csproj src/PrinzipPriceChecker.Api/
RUN dotnet restore src/PrinzipPriceChecker.Api/PrinzipPriceChecker.Api.csproj

COPY src/ src/
RUN dotnet publish src/PrinzipPriceChecker.Api/PrinzipPriceChecker.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

ENV ASPNETCORE_HTTP_PORTS=8080 \
    ConnectionStrings__Default="Data Source=/app/data/pricechecker.db"

# Каталог для файла SQLite создаём заранее и отдаём непривилегированному пользователю:
# именованный том Docker унаследует эти права.
RUN mkdir -p /app/data && chown -R $APP_UID:$APP_UID /app/data

COPY --from=build /app/publish ./

USER $APP_UID
EXPOSE 8080

ENTRYPOINT ["dotnet", "PrinzipPriceChecker.Api.dll"]
