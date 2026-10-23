FROM node:22-alpine AS frontend-build
WORKDIR /frontend

COPY frontend/roamly-web/package.json frontend/roamly-web/package-lock.json ./
RUN npm ci

COPY frontend/roamly-web/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS api-build
WORKDIR /src

COPY . .
# Il pin locale 10.0.204 non e' disponibile nei feed ufficiali; nel container si usa il tag mobile .NET 10.
RUN rm global.json && dotnet publish src/Roamly.Api/Roamly.Api.csproj \
    --configuration Release \
    --output /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

COPY --from=api-build /app/publish/ ./
COPY --from=frontend-build /frontend/dist/ ./wwwroot/

ENTRYPOINT ["dotnet", "Roamly.Api.dll"]
