import { describe, expect, it } from 'vitest'

import { PASSWORD_REQUIREMENTS, passwordMeetsRequirements, passwordsMatch } from '@/lib/password-validation'

function requirement(id: string) {
  const found = PASSWORD_REQUIREMENTS.find((r) => r.id === id)
  if (!found) {
    throw new Error(`No PASSWORD_REQUIREMENTS entry with id "${id}" - did the id change?`)
  }

  return found
}

describe('PASSWORD_REQUIREMENTS', () => {
  it('flags every requirement as unmet against a blank password', () => {
    expect(PASSWORD_REQUIREMENTS.every((r) => !r.test(''))).toBe(true)
  })

  it('marks the length requirement met at exactly 8 characters', () => {
    expect(requirement('length').test('1234567')).toBe(false)
    expect(requirement('length').test('12345678')).toBe(true)
  })

  it('marks the uppercase requirement independently of the other rules', () => {
    expect(requirement('upper').test('all lowercase')).toBe(false)
    expect(requirement('upper').test('has ONE upper')).toBe(true)
  })

  it('marks the lowercase requirement independently of the other rules', () => {
    expect(requirement('lower').test('ALL UPPERCASE')).toBe(false)
    expect(requirement('lower').test('HAS one lower')).toBe(true)
  })

  it('marks the digit requirement independently of the other rules', () => {
    expect(requirement('digit').test('no digits here')).toBe(false)
    expect(requirement('digit').test('has 1 digit')).toBe(true)
  })

  it('marks the special-character requirement independently of the other rules', () => {
    expect(requirement('special').test('nospecialchars123')).toBe(false)
    expect(requirement('special').test('has-a-dash')).toBe(true)
  })
})

describe('passwordMeetsRequirements', () => {
  it('rejects a password missing length', () => {
    expect(passwordMeetsRequirements('Sh0rt!')).toBe(false)
  })

  it('rejects a password with no uppercase letter', () => {
    expect(passwordMeetsRequirements('alllowercase1!')).toBe(false)
  })

  it('rejects a password with no lowercase letter', () => {
    expect(passwordMeetsRequirements('ALLUPPERCASE1!')).toBe(false)
  })

  it('rejects a password with no digit', () => {
    expect(passwordMeetsRequirements('NoDigitsHere!')).toBe(false)
  })

  it('rejects a password with no special character', () => {
    expect(passwordMeetsRequirements('NoSpecial123')).toBe(false)
  })

  it('accepts a password meeting every requirement', () => {
    expect(passwordMeetsRequirements('Str0ng!Pass')).toBe(true)
  })
})

describe('passwordsMatch', () => {
  it('returns true when both values are identical', () => {
    expect(passwordsMatch('Str0ng!Pass', 'Str0ng!Pass')).toBe(true)
  })

  it('returns false when the values differ', () => {
    expect(passwordsMatch('Str0ng!Pass', 'Different1!')).toBe(false)
  })
})
