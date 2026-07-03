const test = require("node:test");
const assert = require("node:assert/strict");
const { createServer, safeStaticPath } = require("../src/server");

function listen(server) {
  return new Promise((resolve) => {
    server.listen(0, "127.0.0.1", () => resolve(server.address().port));
  });
}

async function request(pathname) {
  const server = createServer();
  const port = await listen(server);
  try {
    const response = await fetch(`http://127.0.0.1:${port}${pathname}`);
    return {
      status: response.status,
      contentType: response.headers.get("content-type"),
      body: await response.text()
    };
  } finally {
    await new Promise((resolve) => server.close(resolve));
  }
}

test("serves the landing page", async () => {
  const response = await request("/");
  assert.equal(response.status, 200);
  assert.match(response.contentType, /text\/html/);
  assert.match(response.body, /The all-in-one mod/);
});

test("serves static assets with correct content type", async () => {
  const response = await request("/app.js");
  assert.equal(response.status, 200);
  assert.match(response.contentType, /text\/javascript/);
  assert.match(response.body, /installSteps/);
});

test("blocks path traversal for static assets", () => {
  assert.equal(safeStaticPath("/../../package.json"), null);
});
