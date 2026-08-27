FROM node:22 AS web-build
WORKDIR /src/web/omnibusiness-web

COPY web/omnibusiness-web/package.json web/omnibusiness-web/package-lock.json ./
RUN npm ci

COPY web/omnibusiness-web/ ./
RUN npm run build -- --configuration production

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS dotnet-build
WORKDIR /src

COPY global.json NuGet.Config Directory.Build.props OmniBusiness.slnx ./
COPY src/OmniBusiness.Api/OmniBusiness.Api.csproj src/OmniBusiness.Api/
COPY src/OmniBusiness.Application/OmniBusiness.Application.csproj src/OmniBusiness.Application/
COPY src/OmniBusiness.Domain/OmniBusiness.Domain.csproj src/OmniBusiness.Domain/
COPY src/OmniBusiness.Infrastructure/OmniBusiness.Infrastructure.csproj src/OmniBusiness.Infrastructure/

RUN dotnet restore src/OmniBusiness.Api/OmniBusiness.Api.csproj --configfile NuGet.Config

COPY src/ src/
RUN dotnet publish src/OmniBusiness.Api/OmniBusiness.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    LOCALAPPDATA=/data

COPY --from=dotnet-build /app/publish ./
COPY --from=web-build /src/.artifacts/web-dist/omnibusiness-web/browser/ ./wwwroot/

VOLUME ["/data"]
EXPOSE 8080

ENTRYPOINT ["dotnet", "OmniBusiness.Api.dll"]
