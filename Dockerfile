FROM mcr.microsoft.com/dotnet/sdk:8.0@sha256:df1aebc5fd72a1315f34eda24206f195d5ca00ccf2e3009947a74c5a67166cbb AS publish

ARG BUILD_CONFIGURATION=Release
ARG CI
ARG MINVERVERSIONOVERRIDE

WORKDIR /src

COPY src/DorisStorageAdapter.Helpers/DorisStorageAdapter.Helpers.csproj DorisStorageAdapter.Helpers/
COPY src/DorisStorageAdapter.Server/DorisStorageAdapter.Server.csproj DorisStorageAdapter.Server/
COPY src/DorisStorageAdapter.Services/DorisStorageAdapter.Services.csproj DorisStorageAdapter.Services/
COPY src/Directory.Build.props .
COPY src/Directory.Packages.props .
RUN dotnet restore DorisStorageAdapter.Server/DorisStorageAdapter.Server.csproj

COPY src .
RUN dotnet publish DorisStorageAdapter.Server/DorisStorageAdapter.Server.csproj \
-c $BUILD_CONFIGURATION \
-o /app/publish \
--no-restore \
-p:UseAppHost=false \
-p:MinVerVersionOverride=$MINVERVERSIONOVERRIDE \
-p:CI=$CI

FROM mcr.microsoft.com/dotnet/aspnet:8.0@sha256:ebdd28e9ee54ea5032a390500d37bb1b6d45c36c6ba51e10f3ddfcdc746f3e28 AS final
USER app
WORKDIR /app
EXPOSE 8080
EXPOSE 8081
COPY --from=publish /app/publish .

ENTRYPOINT ["dotnet", "DorisStorageAdapter.Server.dll"]