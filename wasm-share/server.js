const http = require("http");
const { WebSocketServer } = require("ws");
const fs = require("fs");
const path = require("path");

const PORT = 3000;

const MIME = {
  ".html": "text/html",
  ".js": "application/javascript",
  ".wasm": "application/wasm",
  ".css": "text/css",
};

const httpServer = http.createServer((req, res) => {
  let filePath = path.join(__dirname, "public", req.url === "/" ? "index.html" : req.url);
  const ext = path.extname(filePath);
  const contentType = MIME[ext] || "application/octet-stream";

  fs.readFile(filePath, (err, data) => {
    if (err) {
      res.writeHead(404);
      res.end("Not found");
      return;
    }
    res.writeHead(200, { "Content-Type": contentType });
    res.end(data);
  });
});

// Signaling server: rooms indexed by roomId
const rooms = {};

const wss = new WebSocketServer({ server: httpServer });

wss.on("connection", (ws) => {
  let currentRoom = null;

  ws.on("message", (raw) => {
    const msg = JSON.parse(raw);

    if (msg.type === "join") {
      currentRoom = msg.room;
      if (!rooms[currentRoom]) rooms[currentRoom] = [];
      rooms[currentRoom].push(ws);

      // Notify peers already in the room
      rooms[currentRoom].forEach((peer) => {
        if (peer !== ws && peer.readyState === 1) {
          peer.send(JSON.stringify({ type: "peer-joined" }));
        }
      });
      return;
    }

    // Relay offer / answer / candidate to the other peer in the room
    if (currentRoom && rooms[currentRoom]) {
      rooms[currentRoom].forEach((peer) => {
        if (peer !== ws && peer.readyState === 1) {
          peer.send(raw.toString());
        }
      });
    }
  });

  ws.on("close", () => {
    if (currentRoom && rooms[currentRoom]) {
      rooms[currentRoom] = rooms[currentRoom].filter((p) => p !== ws);
      if (rooms[currentRoom].length === 0) delete rooms[currentRoom];
    }
  });
});

httpServer.listen(PORT, () => {
  console.log(`Server running at http://localhost:${PORT}`);
});
