FROM golang:1.26.3-alpine AS build
WORKDIR /src

COPY src/services/PriceFeed.Service/go.mod src/services/PriceFeed.Service/go.sum ./
RUN go mod download

COPY src/services/PriceFeed.Service/ ./
RUN CGO_ENABLED=0 GOOS=linux go build -o /out/pricefeed-service .

FROM scratch AS final
COPY --from=build /out/pricefeed-service /pricefeed-service

ENTRYPOINT ["/pricefeed-service"]
