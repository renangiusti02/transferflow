#!/bin/sh

awslocal sqs create-queue \
  --queue-name transfer-completed