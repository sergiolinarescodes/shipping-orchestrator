/**
 * Shared Tailwind preset for @ship/ui consumers.
 * All values mirror CSS custom properties declared in src/tokens.css —
 * change a token there and Tailwind picks it up via var() reference.
 */
module.exports = {
  theme: {
    extend: {
      colors: {
        "ship-orange": {
          50:  "var(--ship-orange-50)",
          100: "var(--ship-orange-100)",
          200: "var(--ship-orange-200)",
          300: "var(--ship-orange-300)",
          400: "var(--ship-orange-400)",
          500: "var(--ship-orange-500)",
          600: "var(--ship-orange-600)",
          700: "var(--ship-orange-700)",
          800: "var(--ship-orange-800)",
        },
        "ship-navy": {
          50:  "var(--ship-navy-50)",
          100: "var(--ship-navy-100)",
          200: "var(--ship-navy-200)",
          300: "var(--ship-navy-300)",
          500: "var(--ship-navy-500)",
          600: "var(--ship-navy-600)",
          700: "var(--ship-navy-700)",
          800: "var(--ship-navy-800)",
          900: "var(--ship-navy-900)",
        },
        ink: {
          25:  "var(--ink-25)",
          50:  "var(--ink-50)",
          100: "var(--ink-100)",
          150: "var(--ink-150)",
          200: "var(--ink-200)",
          300: "var(--ink-300)",
          400: "var(--ink-400)",
          500: "var(--ink-500)",
          600: "var(--ink-600)",
          700: "var(--ink-700)",
          800: "var(--ink-800)",
          900: "var(--ink-900)",
        },
        green:  { 50: "var(--green-50)",  500: "var(--green-500)",  700: "var(--green-700)" },
        blue:   { 50: "var(--blue-50)",   500: "var(--blue-500)",   700: "var(--blue-700)"  },
        amber:  { 50: "var(--amber-50)",  500: "var(--amber-500)" },
        red:    { 50: "var(--red-50)",    500: "var(--red-500)" },
        purple: { 50: "var(--purple-50)", 500: "var(--purple-500)" },
        canvas: "var(--bg-canvas)",
        border: "var(--border)",
      },
      fontFamily: {
        sans: ["Inter", "Söhne", "-apple-system", "Segoe UI", "Helvetica", "Arial", "sans-serif"],
        display: ["Inter", "Söhne", "-apple-system", "sans-serif"],
        mono: ["JetBrains Mono", "SF Mono", "ui-monospace", "Menlo", "monospace"],
      },
      borderRadius: {
        sm: "var(--r-sm)",
        DEFAULT: "var(--r)",
        md: "var(--r-md)",
        lg: "var(--r-lg)",
        xl: "var(--r-xl)",
      },
      boxShadow: {
        xs: "var(--shadow-xs)",
        sm: "var(--shadow-sm)",
        md: "var(--shadow-md)",
        lg: "var(--shadow-lg)",
      },
      fontFeatureSettings: {
        ship: '"ss01", "cv11", "tnum"',
      },
    },
  },
};
