# Dockerfile — Ntk.Asterisk.WebApi
# Build from repo root:
#   docker build -f src/DotNet/Ntk.Asterisk.WebApi/Dockerfile -t ntk-asterisk-webapi .
# Run:
#   docker run --rm -p 5310:8080 -e ASPNETCORE_ENVIRONMENT=Production ntk-asterisk-webapi

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY src/DotNet/Ntk.AsterNet.AMI/Ntk.AsterNet.AMI.csproj DotNet/Ntk.AsterNet.AMI/
COPY src/DotNet/Ntk.Asterisk.WebApi/Ntk.Asterisk.WebApi.csproj DotNet/Ntk.Asterisk.WebApi/
RUN dotnet restore DotNet/Ntk.Asterisk.WebApi/Ntk.Asterisk.WebApi.csproj
COPY src/DotNet/Ntk.AsterNet.AMI/ DotNet/Ntk.AsterNet.AMI/
COPY src/DotNet/Ntk.Asterisk.WebApi/ DotNet/Ntk.Asterisk.WebApi/
RUN dotnet publish DotNet/Ntk.Asterisk.WebApi/Ntk.Asterisk.WebApi.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080
VOLUME ["/app/App_Data"]
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Ntk.Asterisk.WebApi.dll"]
