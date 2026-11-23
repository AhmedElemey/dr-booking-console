FROM node:22-bookworm-slim AS web
WORKDIR /src/web
COPY web/package.json web/package-lock.json ./
RUN npm ci
COPY web/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS api
WORKDIR /src
COPY Directory.Build.props .editorconfig ./
COPY api/ api/
RUN dotnet publish api/Console.Api.csproj -c Release -o /out

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=api /out ./
COPY --from=web /src/web/dist/clinic-console-web/browser/ ./wwwroot/
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER app
ENTRYPOINT ["dotnet", "Console.Api.dll"]
