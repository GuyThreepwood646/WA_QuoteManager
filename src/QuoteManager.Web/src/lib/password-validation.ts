export interface PasswordRequirement {
  id: string
  label: string
  test: (password: string) => boolean
}

/**
 * Mirrors `PasswordPolicy.Evaluate` on the backend (`QuoteManager.Api.Models.PasswordPolicy`) -
 * keep the two in step if either changes, since the backend is the actual enforcement and this is
 * only the live checklist the user sees while typing.
 */
export const PASSWORD_REQUIREMENTS: PasswordRequirement[] = [
  { id: 'length', label: 'At least 8 characters', test: (password) => password.length >= 8 },
  { id: 'upper', label: 'An uppercase letter', test: (password) => /[A-Z]/.test(password) },
  { id: 'lower', label: 'A lowercase letter', test: (password) => /[a-z]/.test(password) },
  { id: 'digit', label: 'A number', test: (password) => /\d/.test(password) },
  { id: 'special', label: 'A special character', test: (password) => /[^A-Za-z0-9]/.test(password) },
]

export function passwordMeetsRequirements(password: string): boolean {
  return PASSWORD_REQUIREMENTS.every((requirement) => requirement.test(password))
}

export function passwordsMatch(password: string, confirmPassword: string): boolean {
  return password === confirmPassword
}
