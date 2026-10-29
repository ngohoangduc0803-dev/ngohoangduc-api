
# Build ASP.NET Core .NET 10
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

COPY ngohoangduc.csproj ./
RUN dotnet restore ngohoangduc.csproj

COPY . .
RUN dotnet publish ngohoangduc.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

# Run ASP.NET Core
FROM mcr.microsoft.com/dotnet/aspnet:10.0

WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://0.0.0.0:10000
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 10000

ENTRYPOINT ["dotnet", "ngohoangduc.dll"]
