FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

WORKDIR /src

COPY SAED-PortalEmpleado.Api/SAED-PortalEmpleado.Api.csproj SAED-PortalEmpleado.Api/
COPY SAED-PortalEmpleado.Application/SAED-PortalEmpleado.Application.csproj SAED-PortalEmpleado.Application/
COPY SAED-PortalEmpleado.Domain/SAED-PortalEmpleado.Domain.csproj SAED-PortalEmpleado.Domain/
COPY SAED-PortalEmpleado.Infrastructure/SAED-PortalEmpleado.Infrastructure.csproj SAED-PortalEmpleado.Infrastructure/

RUN dotnet restore SAED-PortalEmpleado.Api/SAED-PortalEmpleado.Api.csproj

COPY . .

RUN dotnet publish SAED-PortalEmpleado.Api/SAED-PortalEmpleado.Api.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime

WORKDIR /app

ENV ASPNETCORE_URLS=http://0.0.0.0:5011

EXPOSE 5011

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "SAED-PortalEmpleado.Api.dll"]
