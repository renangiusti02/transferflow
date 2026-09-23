#!/bin/sh

set -e

TABLE_NAME="wallet-activity"

if awslocal dynamodb describe-table \
    --table-name "$TABLE_NAME" >/dev/null 2>&1; then
    exit 0
fi

awslocal dynamodb create-table \
    --table-name "$TABLE_NAME" \
    --attribute-definitions \
        AttributeName=pk,AttributeType=S \
        AttributeName=sk,AttributeType=S \
    --key-schema \
        AttributeName=pk,KeyType=HASH \
        AttributeName=sk,KeyType=RANGE \
    --billing-mode PAY_PER_REQUEST
