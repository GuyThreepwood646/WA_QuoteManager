import { createTheme } from '@mantine/core'

/**
 * The brief asks for clarity and usability over visual polish, so this stays deliberately small:
 * a readable default font stack, slightly tighter spacing than Mantine's default, and nothing that
 * would need maintaining. Status colour lives with the status component, not here, so there is one
 * place that decides what a lifecycle state looks like.
 */
export const theme = createTheme({
  primaryColor: 'indigo',
  defaultRadius: 'md',
  fontFamily:
    '-apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, Helvetica, Arial, sans-serif',
  headings: {
    fontWeight: '600',
  },
})
