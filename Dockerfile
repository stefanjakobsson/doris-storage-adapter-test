FROM mcr.microsoft.com/dotnet/sdk:8.0 AS publish
ARG BUILD_CONFIGURATION=Release

# Build argument for setting the MINVERVERSIONOVERRIDE environment
# variable to allow passing in MinVer version instead of calculating it
# in dotnet publish.
ARG MINVERVERSIONOVERRIDE
ENV MINVERVERSIONOVERRIDE=${MINVERVERSIONOVERRIDE}

WORKDIR /src

COPY src/DorisStorageAdapter.Helpers/DorisStorageAdapter.Helpers.csproj DorisStorageAdapter.Helpers/
COPY src/DorisStorageAdapter.Server/DorisStorageAdapter.Server.csproj DorisStorageAdapter.Server/
COPY src/DorisStorageAdapter.Services/DorisStorageAdapter.Services.csproj DorisStorageAdapter.Services/
COPY src/Directory.Build.props .
RUN dotnet restore DorisStorageAdapter.Server/DorisStorageAdapter.Server.csproj

COPY src .
RUN dotnet publish DorisStorageAdapter.Server/DorisStorageAdapter.Server.csproj -c $BUILD_CONFIGURATION -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
USER app
WORKDIR /app
EXPOSE 8080
EXPOSE 8081
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "DorisStorageAdapter.Server.dll"]