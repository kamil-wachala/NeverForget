FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["src/NeverForget.Contracts/NeverForget.Contracts.csproj", "src/NeverForget.Contracts/"]
COPY ["src/NeverForget.Server/NeverForget.Server.csproj", "src/NeverForget.Server/"]
RUN dotnet restore "src/NeverForget.Server/NeverForget.Server.csproj"

COPY src/NeverForget.Contracts/ src/NeverForget.Contracts/
COPY src/NeverForget.Server/ src/NeverForget.Server/
RUN dotnet publish "src/NeverForget.Server/NeverForget.Server.csproj" -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "NeverForget.Server.dll"]
