/** @type {import('tailwindcss').Config} */
export default {
  content: [
    './src/Client/**/*.{fs,html}'
  ],
  theme: {
    extend: {},
  },
  plugins: [
    require('daisyui')
  ],
  daisyui: {
    themes: ["light", "dark"],
  }
}
