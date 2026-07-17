import { fileURLToPath, URL } from "node:url";
import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

const host = process.env.TAURI_DEV_HOST;

export default defineConfig({
  root: fileURLToPath(new URL("./desktop", import.meta.url)),
  plugins: [react()],
  clearScreen: false,
  build: {
    outDir: fileURLToPath(new URL("./desktop-dist", import.meta.url)),
    emptyOutDir: true,
    sourcemap: true,
  },
  server: {
    port: 1420,
    strictPort: true,
    host: host || false,
    hmr: host
      ? {
          protocol: "ws",
          host,
          port: 1421,
        }
      : undefined,
    watch: {
      ignored: ["**/src-tauri/**", "**/runtime/formula-editor/vendor/**"],
    },
  },
});
