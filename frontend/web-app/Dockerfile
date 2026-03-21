# Stage 1: Build
FROM node:20-alpine AS build
WORKDIR /app

# Accept build-time API URLs (baked into the Next.js bundle)
ARG NEXT_PUBLIC_IDENTITY_URL=http://localhost:5001
ARG NEXT_PUBLIC_MOVIE_URL=http://localhost:5002
ARG NEXT_PUBLIC_RATING_URL=http://localhost:5003
ENV NEXT_PUBLIC_IDENTITY_URL=$NEXT_PUBLIC_IDENTITY_URL
ENV NEXT_PUBLIC_MOVIE_URL=$NEXT_PUBLIC_MOVIE_URL
ENV NEXT_PUBLIC_RATING_URL=$NEXT_PUBLIC_RATING_URL

# Copy package files
COPY package*.json ./

# Install dependencies
RUN npm ci

# Copy source code
COPY . .

# Build the application
RUN npm run build

# Stage 2: Runtime
FROM node:20-alpine
WORKDIR /app

# Install only production dependencies
COPY package*.json ./
RUN npm ci --omit=dev

# Copy built files from the build stage
COPY --from=build /app/.next ./.next
COPY --from=build /app/public ./public

EXPOSE 3000

ENV NODE_ENV=production

CMD ["npm", "start"]