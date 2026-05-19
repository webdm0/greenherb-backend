FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY src/GreenHerb.Api/GreenHerb.Api.csproj src/GreenHerb.Api/
COPY src/GreenHerb.Application/GreenHerb.Application.csproj src/GreenHerb.Application/
COPY src/GreenHerb.Infrastructure/GreenHerb.Infrastructure.csproj src/GreenHerb.Infrastructure/
COPY src/GreenHerb.Domain/GreenHerb.Domain.csproj src/GreenHerb.Domain/
RUN dotnet restore src/GreenHerb.Api/GreenHerb.Api.csproj

COPY src/ src/
RUN dotnet publish src/GreenHerb.Api/GreenHerb.Api.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_FORWARDEDHEADERS_ENABLED=true
ENV PORT=10000

EXPOSE 10000

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "GreenHerb.Api.dll"]
