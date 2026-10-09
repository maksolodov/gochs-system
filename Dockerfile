FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY backend/Gochs.csproj backend/
RUN dotnet restore backend/Gochs.csproj
COPY backend/ backend/
RUN dotnet publish backend/Gochs.csproj -c Release -o /out --no-restore
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /out .
ENV ASPNETCORE_URLS=http://+:8080 DataPath=/app/data
RUN mkdir /app/data /app/backups && chown app:app /app/data /app/backups && chmod 700 /app/data /app/backups
USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "Gochs.dll"]
