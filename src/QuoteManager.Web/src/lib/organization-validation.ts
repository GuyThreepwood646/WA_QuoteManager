import type { OrganizationListItem } from '@/api/types'
import { type FieldErrors, validateEmail, validatePhone, validateRequired } from '@/lib/form-validation'

export interface OrganizationLocationDraft {
  address: string
  phone: string
}

export interface OrganizationDraft {
  name: string
  primaryAddress: string
  primaryContactName: string
  primaryContactEmail: string
  primaryContactPhone: string
  isPreferredVendor: boolean
  locations: OrganizationLocationDraft[]
}

export function emptyOrganizationDraft(): OrganizationDraft {
  return {
    name: '',
    primaryAddress: '',
    primaryContactName: '',
    primaryContactEmail: '',
    primaryContactPhone: '',
    isPreferredVendor: false,
    locations: [{ address: '', phone: '' }],
  }
}

export function organizationToDraft(org: OrganizationListItem): OrganizationDraft {
  return {
    name: org.name,
    primaryAddress: org.primaryAddress ?? '',
    primaryContactName: org.primaryContactName ?? '',
    primaryContactEmail: org.primaryContactEmail ?? '',
    primaryContactPhone: org.primaryContactPhone ?? '',
    isPreferredVendor: org.isPreferredVendor,
    locations:
      org.locations.length > 0
        ? org.locations.map((location) => ({
            address: location.address,
            phone: location.phone ?? '',
          }))
        : [{ address: '', phone: '' }],
  }
}

/** The field-error key for a given location row's phone input - shared with the component that renders it. */
export function locationPhoneErrorKey(index: number): string {
  return `location-${index}-phone`
}

export function validateOrganizationDraft(draft: OrganizationDraft, kind: string): FieldErrors {
  const errors: FieldErrors = {}

  const nameError = validateRequired(draft.name, 'Name')
  if (nameError) {
    errors.name = nameError
  }

  if (draft.primaryContactEmail.trim() !== '') {
    const emailError = validateEmail(draft.primaryContactEmail)
    if (emailError) {
      errors.primaryContactEmail = emailError
    }
  }

  if (draft.primaryContactPhone.trim() !== '') {
    const phoneError = validatePhone(draft.primaryContactPhone)
    if (phoneError) {
      errors.primaryContactPhone = phoneError
    }
  }

  draft.locations.forEach((location, index) => {
    if (location.phone.trim() !== '') {
      const phoneError = validatePhone(location.phone)
      if (phoneError) {
        errors[locationPhoneErrorKey(index)] = phoneError
      }
    }
  })

  if (kind !== 'Vendor' && draft.isPreferredVendor) {
    errors.isPreferredVendor = 'Only vendor organizations can be marked as preferred.'
  }

  return errors
}

export function draftToLocationInputs(draft: OrganizationDraft) {
  return draft.locations
    .map((location) => ({
      address: location.address.trim(),
      phone: location.phone.trim() === '' ? undefined : location.phone.trim(),
    }))
    .filter((location) => location.address !== '')
}
