FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# global.json se copia antes del restore para que restore y publish usen el mismo SDK (10.x).
COPY global.json MyApp.slnx ./
COPY src/Bootstrap/Bootstrap.csproj src/Bootstrap/
COPY src/Modules/Sample/Sample.csproj src/Modules/Sample/
COPY src/Modules/Tasks/Tasks.csproj src/Modules/Tasks/
COPY src/Shared/Shared.csproj src/Shared/
RUN dotnet restore src/Bootstrap/Bootstrap.csproj

COPY . .
RUN dotnet publish src/Bootstrap/Bootstrap.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "MyApp.Bootstrap.dll"]
