# Use the official .NET SDK image to build the app
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

WORKDIR /app

# Copy project file and nuget config first, so the restore layer is cached. The CMF feed needs credentials:
# .nuget-cmf-credentials holds "Username=<user>;Password=<token>" (deploy.ps1 stages it; for a local docker build,
# create it next to the Dockerfile). It only exists in this build stage: the runtime image below copies /out only.
# The [s] pattern makes the file optional (a build without it fails at restore with a 401, not at COPY).
COPY MESSimulator.csproj nuget.config .nuget-cmf-credential[s] ./

# The project file is named: the folder also holds the .sln, and a bare "dotnet publish" would build it, tests included
RUN NuGetPackageSourceCredentials_CMF="$(cat .nuget-cmf-credentials 2>/dev/null)" dotnet restore MESSimulator.csproj \
    && rm -f .nuget-cmf-credentials

# Copy the rest of the source (see .dockerignore: no appsettings.json with the token, no tests) and build the app
COPY . ./
RUN rm -f .nuget-cmf-credentials && dotnet publish MESSimulator.csproj -c Release --no-restore -o /out

# Runtime image
FROM mcr.microsoft.com/dotnet/runtime:8.0

WORKDIR /app

# Copy the published output from build stage
COPY --from=build /out .

# OpenShift runs the container as an arbitrary user of group 0: the app directory must be readable by it, and the
# runtime must not need a writable home directory
# A named culture: the MES client sends its name, and the MES refuses an empty one (the invariant culture's)
ENV LANG=en_US.UTF-8
ENV HOME=/tmp
ENV DOTNET_CLI_HOME=/tmp
ENV DOTNET_BUNDLE_EXTRACT_BASE_DIR=/tmp
RUN chgrp -R 0 /app && chmod -R g=u /app
USER 1001

# The MES address and token come from MESSIM_ environment variables, the line from simulationConfiguration.json (or MESSIM_LINE_FILE)
ENTRYPOINT ["dotnet", "MESSimulator.dll"]
