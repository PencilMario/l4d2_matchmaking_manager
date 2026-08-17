import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

const coreOrigin = process.env.CORE_ORIGIN ?? 'http://127.0.0.1:18080';

export default defineConfig({
  plugins: [react()],
  server: {
    host: '127.0.0.1',
    port: 4173,
    proxy: {
      '/healthz': { target: coreOrigin, changeOrigin: true },
      '/v1': { target: coreOrigin, changeOrigin: true },
    },
  },
});
