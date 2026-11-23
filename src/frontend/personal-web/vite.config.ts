import { defineConfig, loadEnv } from "vite";
import react from "@vitejs/plugin-react";

export default defineConfig(({ mode }) => {
  const environment = loadEnv(mode, ".", "ATTENDANCE_");
  const apiTarget = environment.ATTENDANCE_API_URL || "http://localhost:5081";

  return {
    plugins: [react()],
    server: {
      host: "0.0.0.0",
      port: 5173,
      strictPort: true,
      proxy: {
        "/api": {
          target: apiTarget,
          changeOrigin: true
        }
      }
    }
  };
});
