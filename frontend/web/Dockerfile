# LA-180: web portal image. Build context: frontend/web/ (see docker-compose.yml).
FROM node:24-alpine AS build
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm ci
COPY . .
# The portal calls the API on its own origin; nginx forwards /api and /hubs to the api service.
ARG VITE_API_BASE_URL=/
ARG VITE_AI_ASSISTANT_SEND_ENABLED=false
ENV VITE_API_BASE_URL=$VITE_API_BASE_URL \
    VITE_AI_ASSISTANT_SEND_ENABLED=$VITE_AI_ASSISTANT_SEND_ENABLED
RUN npm run build

FROM nginx:1.27-alpine
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 80
