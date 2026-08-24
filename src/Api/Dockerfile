# Build context is the repository root, so Directory.Build.props and Directory.Packages.props are
# available — central package management needs them at restore time.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/Domain/Kaff.Domain.csproj src/Domain/
COPY src/Infrastructure/Kaff.Infrastructure.csproj src/Infrastructure/
COPY src/Api/Kaff.Api.csproj src/Api/

RUN dotnet restore src/Api/Kaff.Api.csproj

COPY src/ src/
COPY .editorconfig ./

RUN dotnet publish src/Api/Kaff.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# The UI is Arabic and figures are formatted per locale, so the invariant-globalization image is not
# an option here.
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false \
    ASPNETCORE_URLS=http://+:8080

RUN adduser --system --group --no-create-home kaff
USER kaff

COPY --from=build --chown=kaff:kaff /app ./

EXPOSE 8080

# The application refuses to start outside Development when the database guards are missing, so a
# container that stays up is a container whose append-only and balance rules are actually enforced.
ENTRYPOINT ["dotnet", "Kaff.Api.dll"]
