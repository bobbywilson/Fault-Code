FROM qdrant/qdrant:v1.19.2@sha256:b7b0444c4c351c970b98e90a6f89c2ee4287c65b44e52b4cb503fa5b2aa927ad
COPY docker/qdrant-health.sh /usr/local/bin/faultcode-health
HEALTHCHECK --interval=10s --timeout=5s --start-period=15s --retries=6 CMD ["timeout", "3", "bash", "/usr/local/bin/faultcode-health"]
