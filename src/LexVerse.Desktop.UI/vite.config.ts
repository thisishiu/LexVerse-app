import { defineConfig } from "vitest/config";
import react from "@vitejs/plugin-react";

export default defineConfig({
  plugins: [react()],
  base: "/",
  build: {
    outDir: "obj/dist",
    emptyOutDir: true,
    sourcemap: false,
    assetsInlineLimit: 0,
    target: "es2022",
    rollupOptions: { input: { main: "index.html", popup: "popup.html", sample: "sample.html" } }
  },
  test: {
    environment: "jsdom",
    setupFiles: "./src/test/setup.ts",
    globals: true,
    css: true
  }
});
