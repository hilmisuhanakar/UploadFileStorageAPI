# ---------- Build ----------
    FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
    WORKDIR /src
    
    COPY UploadFileStorageAPI.csproj ./
    RUN dotnet restore
    
    COPY . .
    RUN dotnet publish UploadFileStorageAPI.csproj -c Release -o /app/publish \
        --no-restore \
        -p:UseAppHost=false
    
    # ---------- Runtime ----------
    FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
    WORKDIR /app
    COPY --from=build /app/publish .
    
    ENV ASPNETCORE_ENVIRONMENT=Production \
        DOTNET_EnableDiagnostics=0
    
    USER $APP_UID
    
    # Render PORT verir. Yoksa 10000.
    ENTRYPOINT ["sh", "-c", "ASPNETCORE_URLS=http://0.0.0.0:${PORT:-10000} exec dotnet UploadFileStorageAPI.dll"]