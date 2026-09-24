FROM golang:1.26.3-alpine AS build
WORKDIR /src

COPY src/services/Payment.Service/go.mod src/services/Payment.Service/go.sum ./
RUN go mod download

COPY src/services/Payment.Service/ ./
RUN CGO_ENABLED=0 GOOS=linux go build -o /out/payment-service ./cmd/payment-service

FROM scratch AS final
COPY --from=build /out/payment-service /payment-service

ENTRYPOINT ["/payment-service"]

