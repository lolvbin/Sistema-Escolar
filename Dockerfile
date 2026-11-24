# Imagem de produção. Build:  docker build -t sistema-escolar .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY SistemaEscolar.csproj ./
RUN dotnet restore
COPY . .
RUN dotnet publish SistemaEscolar.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_FORWARDEDHEADERS_ENABLED=true \
    DATAPROTECTION_DIR=/chaves
VOLUME /chaves
EXPOSE 8080
ENTRYPOINT ["dotnet", "SistemaEscolar.dll"]
