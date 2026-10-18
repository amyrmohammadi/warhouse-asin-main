export type ColorTheme = 'light' | 'dark'

const themeStorageKey = 'vue-clean-architecture.theme'

export function getStoredTheme(): ColorTheme {
  try {
    return localStorage.getItem(themeStorageKey) === 'dark' ? 'dark' : 'light'
  } catch {
    return 'light'
  }
}

export function applyTheme(theme: ColorTheme): void {
  document.body.dataset.appTheme = theme
  document.body.style.colorScheme = theme
}

export function storeTheme(theme: ColorTheme): void {
  try {
    localStorage.setItem(themeStorageKey, theme)
  } catch {
    // The selected theme still applies when storage is unavailable.
  }

  applyTheme(theme)
}
