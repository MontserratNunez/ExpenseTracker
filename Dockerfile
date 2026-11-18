FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props ExpenseTracker.slnx ./
COPY src/ src/
RUN dotnet restore src/Presentation/ExpenseTracker.Web/ExpenseTracker.Web.csproj
RUN dotnet publish src/Presentation/ExpenseTracker.Web/ExpenseTracker.Web.csproj \
    -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

# Folder for the cookie encryption keys; mount a volume here so sessions survive restarts.
RUN mkdir /keys && chown $APP_UID /keys

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "ExpenseTracker.Web.dll"]
