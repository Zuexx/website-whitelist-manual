import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// https://vite.dev/config/
export default defineConfig({
  // Loaded via file:// (or an asar-transparent equivalent) in the packaged
  // Electron app, not from a server root — an absolute base ("/assets/...")
  // resolves against the filesystem root and 404s there, leaving a blank
  // window with no visible error. Relative asset paths work under both
  // that and the Vite dev server.
  base: "./",
  plugins: [react()],
})
