FROM node:20-alpine AS build
WORKDIR /app
COPY package*.json ./
RUN npm ci
COPY . .
# Create React App bakes this in at build time. Vercel's container build
# does not pass project-configured env vars through as Docker build-args,
# so the default here (not a Vercel project env var) is what actually ships
# in production — and /api is always correct since frontend and backend are
# same-origin. Override only for standalone local runs against a backend
# on a different origin (docker-compose passes its own --build-arg).
ARG REACT_APP_API_URL=/api
ENV REACT_APP_API_URL=$REACT_APP_API_URL
RUN npm run build

FROM nginx:alpine AS final
COPY --from=build /app/build /usr/share/nginx/html
# The platform sets $PORT at runtime; the stock nginx image's entrypoint runs
# envsubst over templates/*.template into conf.d/*.conf before starting, so
# this file can't hardcode a port the way a plain default.conf would.
COPY nginx.conf /etc/nginx/templates/default.conf.template
ENV PORT=8080
EXPOSE 8080
CMD ["nginx", "-g", "daemon off;"]
