export function parseCountryCodes(value: string) {
  return [
    ...new Set(
      value
        .split(/\r?\n/)
        .map((code) => code.trim().toUpperCase())
        .filter(Boolean)
    ),
  ]
}
