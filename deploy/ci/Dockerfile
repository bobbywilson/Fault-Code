# Part of FAU-23. Runs inside the dedicated rootless daemon; no host Docker socket.
FROM docker:29.1.3-cli AS docker_cli
FROM mcr.microsoft.com/dotnet/sdk:10.0.402-noble@sha256:0d99ebf1ccad15037b5c98c9fe8979d6b8dbac23812feae2277a0b277b5c5ffa
RUN apt-get update && DEBIAN_FRONTEND=noninteractive apt-get install -y --no-install-recommends \
    ca-certificates curl git jq unzip python3 python3-venv python3-pip openssh-client sudo \
    libicu74 libssl3t64 libkrb5-3 zlib1g liblttng-ust1t64 \
    && rm -rf /var/lib/apt/lists/*
COPY --from=docker_cli /usr/local/bin/docker /usr/local/bin/docker
COPY --from=docker_cli /usr/local/libexec/docker/cli-plugins /usr/local/libexec/docker/cli-plugins
RUN mkdir -p /runner-template && curl -fsSL --retry 3 \
    https://github.com/actions/runner/releases/download/v2.338.0/actions-runner-linux-x64-2.338.0.tar.gz -o /tmp/runner.tgz \
    && echo 'af4b794c1bc41d73d40535e3fe092a39f9679cd8d965954c2aca25a05ca41d32  /tmp/runner.tgz' | sha256sum -c - \
    && tar xzf /tmp/runner.tgz -C /runner-template && rm /tmp/runner.tgz
ENV RUNNER_ALLOW_RUNASROOT=1 DOTNET_NOLOGO=1 DOTNET_CLI_TELEMETRY_OPTOUT=1
COPY entrypoint.sh /runner-entrypoint
ENTRYPOINT ["/bin/bash", "/runner-entrypoint"]
