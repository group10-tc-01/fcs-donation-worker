FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["src/Fcs.Donation.Worker.Worker/Fcs.Donation.Worker.Worker.csproj", "src/Fcs.Donation.Worker.Worker/"]
COPY ["src/Fcs.Donation.Worker.Application/Fcs.Donation.Worker.Application.csproj", "src/Fcs.Donation.Worker.Application/"]
COPY ["Directory.Build.props", "."]
COPY ["Directory.Packages.props", "."]
RUN dotnet restore "src/Fcs.Donation.Worker.Worker/Fcs.Donation.Worker.Worker.csproj"
COPY . .
WORKDIR "/src/src/Fcs.Donation.Worker.Worker"
RUN dotnet build "Fcs.Donation.Worker.Worker.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "Fcs.Donation.Worker.Worker.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Fcs.Donation.Worker.Worker.dll"]
