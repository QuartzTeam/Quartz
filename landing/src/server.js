const http = require("node:http");
const fs = require("node:fs/promises");
const path = require("node:path");
const { URL } = require("node:url");

const publicDir = path.join(__dirname, "public");
const port = Number.parseInt(process.env.PORT || "3000", 10);

const contentTypes = {
  ".html": "text/html; charset=utf-8",
  ".css": "text/css; charset=utf-8",
  ".js": "text/javascript; charset=utf-8",
  ".json": "application/json; charset=utf-8",
  ".svg": "image/svg+xml; charset=utf-8",
  ".png": "image/png",
  ".ico": "image/x-icon"
};

function sendJson(res, statusCode, payload) {
  const body = JSON.stringify(payload);
  res.writeHead(statusCode, {
    "content-type": contentTypes[".json"],
    "content-length": Buffer.byteLength(body)
  });
  res.end(body);
}

function safeStaticPath(pathname) {
  const decoded = decodeURIComponent(pathname);
  if (decoded.includes("\0")) return null;
  const requested = decoded === "/" ? "/index.html" : decoded;
  const filePath = path.resolve(publicDir, `.${requested}`);
  return filePath === publicDir || filePath.startsWith(`${publicDir}${path.sep}`) ? filePath : null;
}

async function serveStatic(req, res, pathname) {
  const filePath = safeStaticPath(pathname);
  if (!filePath) {
    sendJson(res, 400, { error: "Invalid path" });
    return;
  }

  try {
    const file = await fs.readFile(filePath);
    res.writeHead(200, {
      "content-type": contentTypes[path.extname(filePath)] || "application/octet-stream",
      "cache-control": "no-cache"
    });
    res.end(file);
  } catch (error) {
    if (error && error.code === "ENOENT") {
      const fallback = await fs.readFile(path.join(publicDir, "index.html"));
      res.writeHead(200, { "content-type": contentTypes[".html"] });
      res.end(fallback);
      return;
    }
    sendJson(res, 500, { error: "Unable to read asset" });
  }
}

function createServer() {
  return http.createServer(async (req, res) => {
    if (!req.url || !req.method) {
      sendJson(res, 400, { error: "Bad request" });
      return;
    }

    if (req.method !== "GET" && req.method !== "HEAD") {
      sendJson(res, 405, { error: "Method not allowed" });
      return;
    }

    const url = new URL(req.url, "http://localhost");
    await serveStatic(req, res, url.pathname);
  });
}

if (require.main === module) {
  createServer().listen(port, () => {
    console.log(`Quartz landing app running at http://localhost:${port}`);
  });
}

module.exports = { createServer, safeStaticPath };
