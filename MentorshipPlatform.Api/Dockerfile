FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy solution and project files
COPY MentorshipPlatform.sln ./
COPY MentorshipPlatform.Api/MentorshipPlatform.Api.csproj MentorshipPlatform.Api/
COPY MentorshipPlatform.Application/MentorshipPlatform.Application.csproj MentorshipPlatform.Application/
COPY MentorshipPlatform.Domain/MentorshipPlatform.Domain.csproj MentorshipPlatform.Domain/
COPY MentorshipPlatform.Infrastructure/MentorshipPlatform.Infrastructure.csproj MentorshipPlatform.Infrastructure/
COPY MentorshipPlatform.Identity/MentorshipPlatform.Identity.csproj MentorshipPlatform.Identity/
COPY MentorshipPlatform.Persistence/MentorshipPlatform.Persistence.csproj MentorshipPlatform.Persistence/

# Restore
RUN dotnet restore MentorshipPlatform.sln

# Copy everything and build
COPY . .
RUN dotnet publish MentorshipPlatform.Api/MentorshipPlatform.Api.csproj -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_GCHeapHardLimit=0xA000000
ENV DOTNET_EnableDiagnostics=0

ENTRYPOINT ["dotnet", "MentorshipPlatform.Api.dll"]
