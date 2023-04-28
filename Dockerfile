FROM mcr.microsoft.com/dotnet/sdk:7.0 as build
WORKDIR /src

USER root 

# copy csproj and restore as distinct layers
COPY src .
RUN dotnet restore

RUN dotnet publish -c release -o /app

# final stage/image
FROM mcr.microsoft.com/dotnet/aspnet:7.0

WORKDIR /app
COPY --from=build /app ./

EXPOSE 8051


# Add a new user "upload-dev" with user id 8877
RUN useradd -u 8877 upload-dev

RUN mkdir /var/data && chown -R upload-dev:upload-dev /var/data

USER upload-dev

ENV ASPNETCORE_URLS=http://+:8051

ENTRYPOINT ["dotnet", "DatasetFileUpload.dll"]