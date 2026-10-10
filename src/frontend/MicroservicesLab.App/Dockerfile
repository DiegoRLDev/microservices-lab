FROM node:24-alpine AS build
WORKDIR /app
ENV CI=true

COPY src/frontend/MicroservicesLab.App/package.json src/frontend/MicroservicesLab.App/pnpm-lock.yaml src/frontend/MicroservicesLab.App/pnpm-workspace.yaml ./
RUN corepack enable && pnpm install --frozen-lockfile

COPY src/frontend/MicroservicesLab.App/ ./
RUN pnpm build

FROM nginx:1.27-alpine
COPY src/frontend/MicroservicesLab.App/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist/microserviceslab-app/browser /usr/share/nginx/html

EXPOSE 80
