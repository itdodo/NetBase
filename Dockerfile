# ---------- 前端构建 ----------
FROM node:22-alpine AS web
WORKDIR /web
COPY web/package*.json ./
RUN npm ci || npm install
COPY web/ ./
RUN npm run build

# ---------- 后端构建 ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/ ./
RUN dotnet publish NetBase.Api/NetBase.Api.csproj -c Release -o /app/publish

# ---------- 运行（前端静态文件由 API 托管，单容器部署） ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
COPY --from=web /web/dist ./wwwroot
EXPOSE 8080
ENTRYPOINT ["dotnet", "NetBase.Api.dll"]
