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

## React Bits provenance

The visual components under `src/components/react-bits/` are copied from [DavidHDev/react-bits](https://github.com/DavidHDev/react-bits) at revision `4e0e030193b563be6be33d928f77d0d01cefe237`. The upstream project is MIT licensed with a Commons Clause notice; the copied component README records the source paths and notice. Components are bundled in this project rather than fetched at runtime.
