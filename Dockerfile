FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY PetWork/PetWork.csproj PetWork/
RUN dotnet restore PetWork/PetWork.csproj

COPY PetWork/ PetWork/
RUN dotnet publish PetWork/PetWork.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 10000
USER $APP_UID

ENTRYPOINT ["sh", "-c", "dotnet PetWork.dll --urls http://0.0.0.0:${PORT:-10000}"]
