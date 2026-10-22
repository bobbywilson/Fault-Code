FROM node:22.22.3-bookworm-slim AS build
WORKDIR /docs
COPY package.json package-lock.json ./
RUN npm ci --no-audit --no-fund
COPY site/ site/
RUN cd site && ../node_modules/.bin/mint export --base-path /docs --output /tmp/docs.zip \
    && node -e "const z=require('adm-zip');new z('/tmp/docs.zip').extractAllTo('/export',true)"
FROM nginxinc/nginx-unprivileged:1.28.0-alpine
ARG SOURCE_REVISION=unversioned
LABEL org.opencontainers.image.revision=$SOURCE_REVISION
LABEL org.opencontainers.image.source="https://github.com/bobbywilson/Fault-Code"
COPY --from=build --chown=101:101 /export /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf
USER 101
EXPOSE 8080
HEALTHCHECK --interval=10s --timeout=3s --retries=6 CMD wget -q -O /dev/null http://127.0.0.1:8080/docs/ || exit 1
