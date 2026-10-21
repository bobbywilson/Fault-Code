FROM node:22.22.3-alpine AS build
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm ci --no-audit --no-fund
COPY angular.json tsconfig.json tsconfig.app.json .postcssrc.json ./
COPY src/ src/
COPY public/ public/
RUN npm run build
FROM nginxinc/nginx-unprivileged:1.28.0-alpine AS runtime
ARG SOURCE_REVISION=unversioned
LABEL org.opencontainers.image.revision=$SOURCE_REVISION
LABEL org.opencontainers.image.source="https://github.com/bobbywilson/Fault-Code"
COPY --from=build --chown=101:101 /app/dist/admin/browser /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf
USER 101
EXPOSE 8080
HEALTHCHECK --interval=10s --timeout=3s --retries=6 CMD wget -q -O /dev/null http://127.0.0.1:8080/admin/incidents || exit 1
