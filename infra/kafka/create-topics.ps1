$ErrorActionPreference = "Stop"

$topics = @(
    "order-created",
    "payment-approved",
    "payment-rejected",
    "price-updated"
)

foreach ($topic in $topics) {
    docker exec kafka /opt/kafka/bin/kafka-topics.sh `
        --bootstrap-server localhost:9092 `
        --create `
        --if-not-exists `
        --topic $topic `
        --partitions 3 `
        --replication-factor 1
}

docker exec kafka /opt/kafka/bin/kafka-topics.sh `
    --bootstrap-server localhost:9092 `
    --list
