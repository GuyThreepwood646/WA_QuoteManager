import type { UpdateUserInput, UserListItem } from '@/api/types'
import { type FieldErrors, validateEmail, validatePhone, validateRequired } from '@/lib/form-validation'

export interface UserDraft {
  email: string
  displayName: string
  address: string
  phone: string
  roles: string[]
  organizationId: string
}

export function emptyUserDraft(): UserDraft {
  return { email: '', displayName: '', address: '', phone: '', roles: [], organizationId: '' }
}

export function userToDraft(user: UserListItem): UserDraft {
  return {
    email: user.email,
    displayName: user.displayName,
    address: user.address ?? '',
    phone: user.phone ?? '',
    roles: user.roles,
    organizationId: user.organizationId ?? '',
  }
}

/**
 * Roles/organization are only ever editable by an admin - a non-admin's draft carries them
 * unchanged from `userToDraft`, so there's nothing to validate there for a self-edit.
 */
export function validateUserDraft(draft: UserDraft, isAdminEditing: boolean): FieldErrors {
  const errors: FieldErrors = {}

  const emailError = validateEmail(draft.email)
  if (emailError) {
    errors.email = emailError
  }

  const nameError = validateRequired(draft.displayName, 'Display name')
  if (nameError) {
    errors.displayName = nameError
  }

  if (draft.phone.trim() !== '') {
    const phoneError = validatePhone(draft.phone)
    if (phoneError) {
      errors.phone = phoneError
    }
  }

  if (isAdminEditing) {
    if (draft.roles.length === 0) {
      errors.roles = 'Select at least one role.'
    } else if (draft.organizationId === '' && !(draft.roles.length === 1 && draft.roles[0] === 'Admin')) {
      errors.organizationId = 'Organization is required unless the only role is Admin.'
    }
  }

  return errors
}

export function draftToUpdateInput(draft: UserDraft): UpdateUserInput {
  return {
    email: draft.email.trim(),
    displayName: draft.displayName.trim(),
    address: draft.address.trim() === '' ? undefined : draft.address.trim(),
    phone: draft.phone.trim() === '' ? undefined : draft.phone.trim(),
    roles: draft.roles,
    organizationId: draft.organizationId === '' ? undefined : draft.organizationId,
  }
}
