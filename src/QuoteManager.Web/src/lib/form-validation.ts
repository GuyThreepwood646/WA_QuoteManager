export type FieldErrors = Partial<Record<string, string>>

export function clearFieldError(errors: FieldErrors, field: string): FieldErrors {
  if (!errors[field]) {
    return errors
  }

  const next = { ...errors }
  delete next[field]
  return next
}

export function hasFieldErrors(errors: FieldErrors): boolean {
  return Object.keys(errors).length > 0
}

export function validateRequired(value: string, label = 'This field'): string | undefined {
  if (value.trim() === '') {
    return `${label} is required.`
  }
}

export function validatePositiveAmount(value: string): string | undefined {
  if (value.trim() === '') {
    return 'Amount is required.'
  }

  const amount = Number(value)
  if (!Number.isFinite(amount) || amount <= 0) {
    return 'Enter an amount greater than zero.'
  }
}

export function validateCurrencyCode(value: string): string | undefined {
  if (value.trim() === '') {
    return 'Currency is required.'
  }

  if (!/^[A-Z]{3}$/.test(value.trim())) {
    return 'Enter a three-letter currency code (for example, USD).'
  }
}

export function validateEmail(value: string): string | undefined {
  if (value.trim() === '') {
    return 'Email is required.'
  }

  if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(value.trim())) {
    return 'Enter a valid email address.'
  }
}

export function validatePhone(value: string): string | undefined {
  const trimmed = value.trim()
  if (trimmed === '') {
    return 'Phone number is required.'
  }

  // Accepts common punctuation (spaces, parentheses, dashes, dots, a leading +) but not letters,
  // then separately checks the digit count so "+1 (704) 555-0198" passes and "abc-defg" doesn't.
  if (!/^[+]?[\d\s().-]+$/.test(trimmed)) {
    return 'Enter a valid phone number.'
  }

  const digitCount = trimmed.replace(/\D/g, '').length
  if (digitCount < 7 || digitCount > 15) {
    return 'Enter a valid phone number.'
  }
}
