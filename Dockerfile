FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/TrucoUruguayo.Core/TrucoUruguayo.Core.csproj src/TrucoUruguayo.Core/
COPY src/TrucoUruguayo.Bot/TrucoUruguayo.Bot.csproj src/TrucoUruguayo.Bot/
RUN dotnet restore src/TrucoUruguayo.Bot/TrucoUruguayo.Bot.csproj

COPY src/ src/
RUN dotnet publish src/TrucoUruguayo.Bot/TrucoUruguayo.Bot.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .

ENTRYPOINT ["dotnet", "TrucoUruguayo.Bot.dll"]
