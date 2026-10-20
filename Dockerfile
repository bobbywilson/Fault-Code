FROM mcr.microsoft.com/dotnet/sdk:10.0.402-noble@sha256:0d99ebf1ccad15037b5c98c9fe8979d6b8dbac23812feae2277a0b277b5c5ffa AS build
WORKDIR /source
COPY global.json Directory.Build.props ./
COPY src/ src/
RUN dotnet restore src/FaultCode.Api/FaultCode.Api.csproj --locked-mode \
    && dotnet publish src/FaultCode.Api/FaultCode.Api.csproj -c Release --no-restore -o /app
COPY docker/HealthProbe/ docker/HealthProbe/
RUN dotnet publish docker/HealthProbe/HealthProbe.csproj -c Release -o /probe

# Explicit test target inspects the entire filtered context, including build-stage inputs.
FROM build AS context-check
COPY . /context
RUN for path in /context/src/FaultCode.Api/.env.smoke /context/.env /context/.git /context/src/FaultCode.Api/bin; do \
      if test -e "$path"; then echo "Unexpected build input: $path"; exit 1; fi; \
    done

FROM mcr.microsoft.com/dotnet/aspnet:10.0.13-noble@sha256:d5633778c754e89a386882eddf8dbee6942447a8a4b8267c19cf1e1158d0b0ac AS runtime
WORKDIR /app
COPY --from=build /app ./
COPY --from=build /probe /probe
ENV ASPNETCORE_HTTP_PORTS=8080
USER $APP_UID
EXPOSE 8080
HEALTHCHECK --interval=10s --timeout=5s --start-period=15s --retries=6 CMD ["dotnet", "/probe/HealthProbe.dll", "http://127.0.0.1:8080/health/live"]
ENTRYPOINT ["dotnet", "FaultCode.Api.dll"]

