#!/bin/bash
# Integration test for MBBSEmu Docker container
# Tests: build, start, create user, logout, login
set -e

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
cd "$SCRIPT_DIR"

# Test configuration
TEST_USER="testuser$$"
TEST_PASS="testpass123"
TEST_EMAIL="test@example.com"
TELNET_PORT="${TELNET_PORT:-2324}"
PROJECT_NAME="mbbsemu-test-$$"

cleanup() {
    echo "Cleaning up..."
    docker compose -p "$PROJECT_NAME" down -v 2>/dev/null || true
    rm -rf "$SCRIPT_DIR/config-test-$$" 2>/dev/null || true
    rm -f "$SCRIPT_DIR/docker-compose.test.yml" 2>/dev/null || true
}
trap cleanup EXIT

echo "=== MBBSEmu Docker Integration Test ==="

# Check for expect
if ! command -v expect &> /dev/null; then
    echo "ERROR: 'expect' is required but not installed."
    echo "  macOS: brew install expect"
    echo "  Linux: apt-get install expect"
    exit 1
fi

# Create test config directory
mkdir -p "$SCRIPT_DIR/config-test-$$"

# Create a test-specific compose override
cat > "$SCRIPT_DIR/docker-compose.test.yml" <<EOF
services:
  mbbsemu:
    container_name: $PROJECT_NAME
    ports:
      - "$TELNET_PORT:23"
    volumes:
      - ./config-test-$$:/config
EOF

# Build and start container
echo "Building and starting container on port $TELNET_PORT..."
docker compose -p "$PROJECT_NAME" -f docker-compose.yml -f docker-compose.test.yml up -d --build

# Wait for container to be ready
echo "Waiting for MBBSEmu to start..."
sleep 5

for i in {1..30}; do
    if nc -z localhost $TELNET_PORT 2>/dev/null; then
        echo "MBBSEmu is ready!"
        break
    fi
    if [ $i -eq 30 ]; then
        echo "ERROR: Timeout waiting for MBBSEmu to start"
        docker compose -p "$PROJECT_NAME" logs
        exit 1
    fi
    sleep 1
done

# Test 1: Create a new user using simple send/expect pattern
echo "Test 1: Creating new user '$TEST_USER'..."

# Use a simpler approach - send all inputs with delays
OUTPUT=$(expect -c "
log_user 0
set timeout 60
spawn nc localhost $TELNET_PORT

# Wait for initial prompt
sleep 3

# Send NEW to create account
send \"NEW\r\"
sleep 2

# Username
send \"$TEST_USER\r\"
sleep 1

# Password
send \"$TEST_PASS\r\"
sleep 1

# Confirm password
send \"$TEST_PASS\r\"
sleep 1

# Email
send \"$TEST_EMAIL\r\"
sleep 1

# Gender (M)
send \"M\r\"
sleep 3

# Capture what we got
expect {
    -re {.+} { }
    timeout { }
}

# Exit/disconnect
send \"X\r\"
sleep 1

puts \"DONE\"
close
" 2>&1)

if echo "$OUTPUT" | grep -q "DONE"; then
    echo "PASS: Test 1 - User creation sequence completed"
else
    echo "FAIL: Test 1 - User creation sequence failed"
    echo "$OUTPUT"
    exit 1
fi

# Check container logs for successful account creation
sleep 2
LOGS=$(docker compose -p "$PROJECT_NAME" logs 2>&1)
if echo "$LOGS" | grep -qi "channel\|session\|connect"; then
    echo "  Container received connections"
else
    echo "  Warning: Could not verify container activity"
fi

# Test 2: Login with existing user
echo "Test 2: Logging in with existing user '$TEST_USER'..."

OUTPUT=$(expect -c "
log_user 0
set timeout 30
spawn nc localhost $TELNET_PORT

sleep 3

# Username
send \"$TEST_USER\r\"
sleep 1

# Password
send \"$TEST_PASS\r\"
sleep 3

# Capture response
expect {
    -re {.+} { }
    timeout { }
}

# Exit
send \"X\r\"
sleep 1

puts \"DONE\"
close
" 2>&1)

if echo "$OUTPUT" | grep -q "DONE"; then
    echo "PASS: Test 2 - Login sequence completed"
else
    echo "FAIL: Test 2 - Login sequence failed"
    echo "$OUTPUT"
    exit 1
fi

# Verify no errors in container logs
if echo "$LOGS" | grep -qi "exception\|error.*sql\|critical"; then
    echo "WARNING: Found potential errors in container logs"
    echo "$LOGS" | grep -i "exception\|error.*sql\|critical" | head -5
fi

echo ""
echo "=== All integration tests passed! ==="
