#!/bin/bash
set -e

# Get the directory where this script is located
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

# Build Wasm if needed
echo "Building WebAssembly module..."
npm run build:wasm 2>/dev/null || echo "⚠ Wasm build skipped"

# Start server in background
echo "Starting WasmShare server..."
node server.js &
SERVER_PID=$!

# Wait for server to be ready
sleep 2

# Open browser (cross-platform)
if command -v xdg-open &> /dev/null; then
  # Linux
  xdg-open "http://localhost:3000" &
elif command -v open &> /dev/null; then
  # macOS
  open "http://localhost:3000" &
else
  echo "✓ Server running at http://localhost:3000"
fi

# Keep server running
wait $SERVER_PID
