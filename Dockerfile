# TinyCRM: Vue SPA built into the API's wwwroot, served by one ASP.NET process.

FROM node:22-alpine AS web
WORKDIR /src/src/tiny-crm-web
COPY src/tiny-crm-web/package.json src/tiny-crm-web/package-lock.json ./
RUN npm ci
COPY src/tiny-crm-web/ ./
# vite.config.ts writes the build to ../TinyCrm.Api/wwwroot
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS api
WORKDIR /src
COPY global.json ./
COPY src/TinyCrm.Api/TinyCrm.Api.csproj src/TinyCrm.Api/
RUN dotnet restore src/TinyCrm.Api/TinyCrm.Api.csproj
COPY src/TinyCrm.Api/ src/TinyCrm.Api/
COPY --from=web /src/src/TinyCrm.Api/wwwroot src/TinyCrm.Api/wwwroot
RUN dotnet publish src/TinyCrm.Api/TinyCrm.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
# Login-cookie signing keys land here by default; compose mounts a volume on it so
# a restart or redeploy does not log everyone out. Pre-created so the volume is
# owned by the non-root app user.
RUN mkdir -p /home/app/.aspnet/DataProtection-Keys && chown -R app:app /home/app/.aspnet
USER app
COPY --from=api /app .
ENTRYPOINT ["dotnet", "TinyCrm.Api.dll"]
