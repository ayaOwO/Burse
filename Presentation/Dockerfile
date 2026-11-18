FROM mcr.microsoft.com/dotnet/sdk:11.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore Presentation/Presentation.csproj
RUN dotnet publish Presentation/Presentation.csproj --no-restore -o /out \
    && mkdir -p /out/logs

FROM mcr.microsoft.com/dotnet/aspnet:11.0
WORKDIR /app
COPY --from=build --chown=app:app /out/ ./
USER app
ENTRYPOINT ["dotnet", "Presentation.dll"]
