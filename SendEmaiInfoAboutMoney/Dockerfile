# Stage 1: build
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# copy and restore
COPY SendEmaiInfoAboutMoney.csproj ./
RUN dotnet restore

# copy the rest and publish
COPY . .
RUN dotnet publish  -c Release -o /app/publish

# Stage 2: runtime
FROM mcr.microsoft.com/dotnet/runtime:9.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "SendEmaiInfoAboutMoney.dll"]