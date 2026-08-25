# --platform=$BUILDPLATFORM: the build stage runs on the RUNNER's architecture. Angular's output is
# JavaScript, CSS and HTML — identical whatever machine emits it — so there is nothing to
# cross-compile and no reason to emulate. Only the nginx runtime stage below is built per target.
#
# Without this, a multi-platform build runs `npm ci` and the whole Angular build under QEMU for the
# arm64 leg, which is slow and pulls in platform-specific native modules for no benefit.
FROM --platform=$BUILDPLATFORM node:22-alpine AS build
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

# The BROWSER subdirectory. Angular's application builder emits
# dist/kaff-web/browser/index.html, and nginx.conf.template sets `root /usr/share/nginx/html`, so
# what is copied here must already be the site root.
#
# This copied the parent until 2026-08-25, which put index.html at
# /usr/share/nginx/html/browser/index.html and made nginx return 404 for `/` — the whole application.
# It survived because the deploy workflow had never run. The identical mistake was live in
# .github/workflows/ci.yml's artifact path and was found the first time CI reached the e2e job.
COPY --from=build /source/dist/kaff-web/browser /usr/share/nginx/html

EXPOSE 8080
