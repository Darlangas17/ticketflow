# syntax=docker/dockerfile:1

# ---------- Build ----------
FROM node:22-alpine AS build
WORKDIR /app

# npm 11 is required (npm 10 cannot read this lockfile; see the README).
RUN npm install --global npm@11.6.2

COPY package.json package-lock.json ./
RUN npm ci --no-audit --no-fund

COPY . .
RUN npx ng build --configuration production

# ---------- Runtime ----------
# Unprivileged nginx: runs as a non-root user and listens on 8080.
FROM nginxinc/nginx-unprivileged:1.29-alpine AS runtime
COPY nginx/default.conf /etc/nginx/conf.d/default.conf
COPY nginx/security-headers.conf /etc/nginx/snippets/security-headers.conf
COPY --from=build /app/dist/ticketflow-web/browser /usr/share/nginx/html
EXPOSE 8080
