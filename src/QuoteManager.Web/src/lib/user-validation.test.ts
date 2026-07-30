import { describe, expect, it } from 'vitest'

import type { UserListItem } from '@/api/types'
import {
  draftToUpdateInput,
  emptyUserDraft,
  userToDraft,
  validateUserDraft,
} from '@/lib/user-validation'

const sampleUser: UserListItem = {
  id: 'user-1',
  email: 'requester@warehouseanywhere.test',
  displayName: 'Riley Requester',
  roles: ['Requester'],
  organizationId: 'org-1',
  organizationName: 'Meridian Pharma Sampling',
  address: '1200 Peachtree Industrial Blvd, Suite 400, Atlanta, GA 30341',
  phone: '+1 (404) 555-0133',
}

describe('userToDraft', () => {
  it('maps every field from the API item, including roles and organization', () => {
    const draft = userToDraft(sampleUser)

    expect(draft.email).toBe('requester@warehouseanywhere.test')
    expect(draft.displayName).toBe('Riley Requester')
    expect(draft.roles).toEqual(['Requester'])
    expect(draft.organizationId).toBe('org-1')
    expect(draft.phone).toBe('+1 (404) 555-0133')
  })

  it('maps a null organization/address/phone to empty strings', () => {
    const draft = userToDraft({ ...sampleUser, organizationId: null, address: null, phone: null })

    expect(draft.organizationId).toBe('')
    expect(draft.address).toBe('')
    expect(draft.phone).toBe('')
  })
})

describe('emptyUserDraft', () => {
  it('starts blank with no roles selected', () => {
    const draft = emptyUserDraft()

    expect(draft.email).toBe('')
    expect(draft.roles).toEqual([])
    expect(draft.organizationId).toBe('')
  })
})

describe('validateUserDraft', () => {
  it('requires a display name', () => {
    const errors = validateUserDraft({ ...userToDraft(sampleUser), displayName: '   ' }, false)

    expect(errors.displayName).toBe('Display name is required.')
  })

  it('rejects an invalid email', () => {
    const errors = validateUserDraft({ ...userToDraft(sampleUser), email: 'not-an-email' }, false)

    expect(errors.email).toBe('Enter a valid email address.')
  })

  it('accepts a blank phone, since it is optional', () => {
    const errors = validateUserDraft({ ...userToDraft(sampleUser), phone: '' }, false)

    expect(errors.phone).toBeUndefined()
  })

  it('rejects a malformed phone', () => {
    const errors = validateUserDraft({ ...userToDraft(sampleUser), phone: 'call me maybe' }, false)

    expect(errors.phone).toBe('Enter a valid phone number.')
  })

  it('does not validate roles/organization for a non-admin self-edit', () => {
    const errors = validateUserDraft({ ...userToDraft(sampleUser), roles: [], organizationId: '' }, false)

    expect(errors.roles).toBeUndefined()
    expect(errors.organizationId).toBeUndefined()
  })

  it('requires at least one role when an admin is editing', () => {
    const errors = validateUserDraft({ ...userToDraft(sampleUser), roles: [] }, true)

    expect(errors.roles).toBe('Select at least one role.')
  })

  it('requires an organization unless the only role is Admin', () => {
    const errors = validateUserDraft(
      { ...userToDraft(sampleUser), roles: ['Requester'], organizationId: '' },
      true,
    )

    expect(errors.organizationId).toBe('Organization is required unless the only role is Admin.')
  })

  it('allows a blank organization when the only role is Admin', () => {
    const errors = validateUserDraft(
      { ...userToDraft(sampleUser), roles: ['Admin'], organizationId: '' },
      true,
    )

    expect(errors.organizationId).toBeUndefined()
  })
})

describe('draftToUpdateInput', () => {
  it('trims strings and omits blank optional fields', () => {
    const input = draftToUpdateInput({
      email: '  requester@warehouseanywhere.test  ',
      displayName: '  Riley Requester  ',
      address: '   ',
      phone: '  +1 (404) 555-0133  ',
      roles: ['Requester'],
      organizationId: 'org-1',
    })

    expect(input.email).toBe('requester@warehouseanywhere.test')
    expect(input.displayName).toBe('Riley Requester')
    expect(input.address).toBeUndefined()
    expect(input.phone).toBe('+1 (404) 555-0133')
    expect(input.organizationId).toBe('org-1')
  })

  it('maps a blank organizationId to undefined', () => {
    const input = draftToUpdateInput({ ...userToDraft(sampleUser), organizationId: '' })

    expect(input.organizationId).toBeUndefined()
  })
})
