FROM mcr.microsoft.com/dotnet/sdk:10.0

SHELL ["/bin/bash", "-lc"]

WORKDIR /src
ENV DALAMUD_HOME=/dalamud

COPY Callouts/ ./Callouts/
COPY Callouts.Tests/ ./Callouts.Tests/
COPY scyt.repo.json ./scyt.repo.json

CMD set -euo pipefail \
    && dotnet restore Callouts.Tests/Callouts.Tests.csproj \
    && dotnet test Callouts.Tests/Callouts.Tests.csproj --no-restore --configuration Release \
    && if [[ ! -d "${DALAMUD_HOME}" ]]; then \
           echo "Tests passed, but plugin build requires a mounted DALAMUD_HOME at ${DALAMUD_HOME}."; \
           echo "Mount a Dalamud dev folder into /dalamud and rerun the container."; \
           exit 2; \
       fi \
    && dotnet restore Callouts/Callouts.csproj -p:EnableWindowsTargeting=true \
    && dotnet build Callouts/Callouts.csproj --no-restore --configuration Release -p:EnableWindowsTargeting=true -o /tmp/plugin-build \
    && if [[ -d /out && -w /out ]]; then \
           rm -rf /out/plugin && mkdir -p /out/plugin && cp -R /tmp/plugin-build/. /out/plugin/; \
           echo "Validation succeeded. Exported build output to /out/plugin."; \
       else \
           echo "Validation succeeded. No artifact export requested."; \
       fi
