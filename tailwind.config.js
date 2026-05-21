/******** Tailwind config for Blazor ********/
/** @type {import('tailwindcss').Config} */
module.exports = {
  content: [
    "./**/*.html",
    "./**/*.razor",
    "./**/*.cshtml",
  ],
  theme: {
    extend: {
      colors: {
        white: '#FFFFFF',
        burgundy: '#682021',
        black: '#0C0C0C',
        yellow: '#EFD867',
      },
      fontFamily: {
        sans: ['Manrope', 'sans-serif'],
      }
    },
  },
  plugins: [],
};