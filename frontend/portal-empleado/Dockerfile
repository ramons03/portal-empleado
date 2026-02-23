FROM node:20 AS build

WORKDIR /app

COPY frontend/portal-empleado/package.json frontend/portal-empleado/package-lock.json ./
RUN npm ci

COPY frontend/portal-empleado ./

RUN npm run build

FROM nginx:1.25-alpine

ENV API_UPSTREAM=https://api.mi.saed.digital

COPY frontend/portal-empleado/nginx.conf.template /etc/nginx/templates/default.conf.template
COPY --from=build /app/dist /usr/share/nginx/html

EXPOSE 80

CMD ["/bin/sh", "-c", "envsubst '$API_UPSTREAM' < /etc/nginx/templates/default.conf.template > /etc/nginx/conf.d/default.conf && nginx -g 'daemon off;'"]
