# Burse Angular workspace

See the [root README](../README.md) for installation, API setup, trader IDs, trading rules, and test instructions.

```sh
npm ci
npm start
```

Development: http://127.0.0.1:4200. The API must be running on port 5243.

```sh
npm run build
npm run test:unit
npm exec playwright install chromium
npm run test:e2e
```

Build the backend first for browser tests. Tests use isolated ports 4201 and 5244. Production output is `dist/burse/browser/`; configure SPA fallback and an `/api` reverse proxy when hosting.
