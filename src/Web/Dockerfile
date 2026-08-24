FROM node:22-alpine AS build
WORKDIR /source

COPY package.json package-lock.json ./
RUN npm ci

COPY . .
RUN npm run build

FROM nginx:1.27-alpine AS runtime

# The nginx image renders /etc/nginx/templates/*.template into /etc/nginx/conf.d at start-up.
# The filter keeps envsubst away from nginx's own $uri, $host and $scheme.
ENV NGINX_ENVSUBST_FILTER="KAFF_" \
    KAFF_API_URL="http://api:8080/api/"

COPY nginx.conf.template /etc/nginx/templates/default.conf.template
COPY --from=build /source/dist/kaff-web /usr/share/nginx/html

EXPOSE 8080
