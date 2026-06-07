/** @type {import('tailwindcss').Config} */
export default {
  content: [
    "./index.html",
    "./src/**/*.{js,ts,jsx,tsx}",
  ],
  darkMode: "class",
  theme: {
    extend: {
      colors: {
        // Gold brand palette — deep enough at 600/700 for readable white-on-gold buttons.
        primary: {
          50: '#fbf7ec',
          100: '#f6ebca',
          200: '#ecd595',
          300: '#dfbb5c',
          400: '#cda033',
          500: '#b8860b',
          600: '#9a6f0c',
          700: '#7c590f',
          800: '#664a13',
          900: '#553e14',
          950: '#2f2107',
        },
        accent: {
          50: '#f0fdf4',
          100: '#dcfce7',
          200: '#bbf7d0',
          300: '#86efac',
          400: '#4ade80',
          500: '#22c55e',
          600: '#16a34a',
          700: '#15803d',
          800: '#166534',
          900: '#14532d',
        },
      },
      fontFamily: {
        sans: ['Inter', 'system-ui', '-apple-system', 'sans-serif'],
      },
    },
  },
  plugins: [],
}
