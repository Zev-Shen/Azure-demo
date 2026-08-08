# ---- Build Stage ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy csproj and restore
COPY src/CloudNotes.Report/CloudNotes.Report.csproj src/CloudNotes.Report/
RUN dotnet restore src/CloudNotes.Report/CloudNotes.Report.csproj

# Copy all source and publish
COPY src/CloudNotes.Report/ src/CloudNotes.Report/
RUN dotnet publish src/CloudNotes.Report/CloudNotes.Report.csproj -c Release -o /app/publish --no-restore

# ---- Runtime Stage ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
EXPOSE 8080

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "CloudNotes.Report.dll"]
