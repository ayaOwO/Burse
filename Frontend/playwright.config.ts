import { defineConfig } from '@playwright/test';
export default defineConfig({
  testDir: './tests/e2e',
  fullyParallel: false,
  workers: 1,
  timeout: 45000,
  use: {
    baseURL: 'http://127.0.0.1:4201',
    browserName: 'chromium',
    viewport: { width: 1440, height: 1000 },
    trace: 'retain-on-failure',
  },
  webServer: [
    {
      command:
        'dotnet run --project ../Presentation --no-build --no-launch-profile --urls http://127.0.0.1:5244',
      url: 'http://127.0.0.1:5244/stocks',
      timeout: 60000,
      env: {
        ASPNETCORE_ENVIRONMENT: 'Development',
        StockUpdaterServiceOptions__TickInterval: '01:00:00',
      },
    },
    {
      command: 'npm start -- --port 4201',
      url: 'http://127.0.0.1:4201',
      timeout: 60000,
      env: { BURSE_API_URL: 'http://127.0.0.1:5244' },
    },
  ],
});
