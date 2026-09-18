#!/bin/sh

set -e

DLQ_URL=$(
  awslocal sqs create-queue \
    --queue-name transfer-completed-dlq \
    --query QueueUrl \
    --output text
)

DLQ_ARN=$(
  awslocal sqs get-queue-attributes \
    --queue-url "$DLQ_URL" \
    --attribute-names QueueArn \
    --query 'Attributes.QueueArn' \
    --output text
)

MAIN_QUEUE_URL=$(
  awslocal sqs create-queue \
    --queue-name transfer-completed \
    --query QueueUrl \
    --output text
)

cat > /tmp/redrive.json <<EOF
{
  "RedrivePolicy": "{\"deadLetterTargetArn\":\"$DLQ_ARN\",\"maxReceiveCount\":3}"
}
EOF

awslocal sqs set-queue-attributes \
  --queue-url "$MAIN_QUEUE_URL" \
  --attributes file:///tmp/redrive.json