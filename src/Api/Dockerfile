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

COPY --from=build /app ./

# The base image already ships a non-root user — `app`, uid 1654, exposed as $APP_UID
# [Verified: 2026-08-25 by `docker run --rm mcr.microsoft.com/dotnet/aspnet:10.0 id $APP_UID`].
#
# This ran `adduser --system --group --no-create-home kaff` until 2026-08-25 and **failed the image
# build entirely** with exit 127: the .NET 10 runtime image is Azure Linux based, not Debian, and has
# no `adduser`. So the line was both broken and unnecessary — it was creating a user the image had
# already created.
#
# After the COPY, deliberately: the copy runs as root and the process then drops to a user with read
# access, which is all it needs. Switching first would require a --chown and buy nothing.
USER $APP_UID

EXPOSE 8080

# The application refuses to start outside Development when the database guards are missing, so a
# container that stays up is a container whose append-only and balance rules are actually enforced.
ENTRYPOINT ["dotnet", "Kaff.Api.dll"]
