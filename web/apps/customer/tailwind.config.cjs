const shipPreset = require("@ship/ui/tailwind-preset");

/** @type {import('tailwindcss').Config} */
module.exports = {
  presets: [shipPreset],
  content: [
    "./index.html",
    "./src/**/*.{ts,tsx}",
    "../../packages/ui/src/**/*.{ts,tsx}",
  ],
};
