FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY DeepScan.csproj ./
RUN dotnet restore
COPY Program.cs index.html ./
COPY cyber-safety-toolkit ./cyber-safety-toolkit
COPY network-scanner ./network-scanner
RUN dotnet publish DeepScan.csproj --configuration Release --no-restore --output /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/publish ./
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "DeepScan.dll"]
