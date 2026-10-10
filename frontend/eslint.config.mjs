import { defineConfig, globalIgnores } from "eslint/config";
import nextVitals from "eslint-config-next/core-web-vitals";
import nextTs from "eslint-config-next/typescript";

// Colores de la paleta de Tailwind: usarlos directamente rompería el cambio de marca por tokens.
const PALETA =
  "red|blue|green|yellow|gray|slate|zinc|neutral|stone|orange|amber|lime|emerald|teal|cyan|sky|indigo|violet|purple|fuchsia|pink|rose|white|black";

const eslintConfig = defineConfig([
  ...nextVitals,
  ...nextTs,
  {
    // Rebranding sin tocar lógica: los colores salen de los tokens de globals.css.
    // Solo components/ui y components/layout pueden contener decisiones visuales propias.
    files: ["src/**/*.{ts,tsx}"],
    ignores: ["src/components/ui/**", "src/components/layout/**", "src/**/*.test.{ts,tsx}"],
    rules: {
      "no-restricted-syntax": [
        "error",
        {
          selector: "Literal[value=/(#[0-9a-fA-F]{6}\\b|\\b(rgb|hsl|oklch)a?\\()/]",
          message: "No use colores fijos; use los tokens del tema (bg-primary, text-muted-foreground, …).",
        },
        {
          selector: `Literal[value=/\\b(bg|text|border|ring|fill|stroke|from|to|via)-(${PALETA})(-\\d{2,3})?\\b/]`,
          message: "No use colores de la paleta de Tailwind; use los tokens del tema (bg-primary, text-destructive, …).",
        },
      ],
    },
  },
  // Override default ignores of eslint-config-next.
  globalIgnores([
    // Default ignores of eslint-config-next:
    ".next/**",
    "out/**",
    "build/**",
    "next-env.d.ts",
  ]),
]);

export default eslintConfig;
