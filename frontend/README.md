# L4D2 Matchmaking Manager Frontend

Desktop-only React management workspace for the Core Controller. Supported viewports begin at `1280x720`; no mobile layout is provided.

## Local development

```powershell
npm ci
$env:CORE_ORIGIN = "http://127.0.0.1:18080"
npm run dev
```

The Vite development server proxies `/healthz` and `/v1` to `CORE_ORIGIN`. Production deployment must expose the static assets and Core routes through the same-origin reverse proxy; the browser does not call Agent APIs or A2S directly.

## Verification

```powershell
npm run test -- --run
npm run build
npm run test:e2e
```

界面不使用 Three.js、Canvas、WebGL 或持续运行的装饰动画。所有管理页面以表格、抽屉、确认对话框和行级状态反馈为主。
