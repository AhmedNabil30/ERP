# Build context is the repository root, so Directory.Build.props and Directory.Packages.props are
# available — central package management needs them at restore time.

# --platform=$BUILDPLATFORM pins the SDK stage to the RUNNER's architecture, and `-a $TARGETARCH`
# cross-compiles from there. Staging is an ARM64 machine (Oracle Cloud) and CI runners are amd64.
#
# The alternative — emulating arm64 with QEMU and building natively — also works and is much slower:
# a .NET restore and publish under emulation is minutes, not seconds, on every push. Cross-compiling
# is what the SDK is for. No QEMU is set up in the workflow, deliberately, and none is needed: the
# runtime stage below only COPYs, so nothing built for the target ever executes during the build.
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG TARGETARCH
WORKDIR /source

COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/Domain/Kaff.Domain.csproj src/Domain/
COPY src/Infrastructure/Kaff.Infrastructure.csproj src/Infrastructure/
COPY src/Api/Kaff.Api.csproj src/Api/

# The restore must name the same architecture as the publish, or the publish restores again and
# --no-restore fails on assets that were resolved for the wrong RID.
RUN dotnet restore src/Api/Kaff.Api.csproj -a $TARGETARCH

COPY src/ src/
COPY .editorconfig ./

RUN dotnet publish src/Api/Kaff.Api.csproj -c Release -a $TARGETARCH -o /app --no-restore

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
