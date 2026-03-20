# Stage 1: Build
FROM node:20-alpine AS build
WORKDIR /app

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