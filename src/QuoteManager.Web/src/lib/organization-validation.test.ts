import { describe, expect, it } from 'vitest'

import type { OrganizationListItem } from '@/api/types'
import {
  draftToLocationInputs,
  emptyOrganizationDraft,
  locationPhoneErrorKey,
  organizationToDraft,
  validateOrganizationDraft,
} from '@/lib/organization-validation'

const sampleOrg: OrganizationListItem = {
  id: 'org-1',
  name: 'SecureBase Self Storage',
  kind: 'Vendor',
  retiredAt: null,
  primaryAddress: '7420 Industrial Park Road, Charlotte, NC 28213',
  primaryContactName: 'Alex Rivera',
  primaryContactEmail: 'alex.rivera@securebase.test',
  primaryContactPhone: '+1 (704) 555-0198',
  isPreferredVendor: true,
  locations: [
    {
      id: 'loc-1',
      address: '910 Logistics Way, Raleigh, NC 27603',
      phone: '+1 (919) 555-0148',
      sortOrder: 0,
    },
  ],
}

describe('organizationToDraft', () => {
  it('maps profile fields and location phones from the API item', () => {
    const draft = organizationToDraft(sampleOrg)

    expect(draft.name).toBe('SecureBase Self Storage')
    expect(draft.primaryContactEmail).toBe('alex.rivera@securebase.test')
    expect(draft.isPreferredVendor).toBe(true)
    expect(draft.locations).toEqual([
      { address: '910 Logistics Way, Raleigh, NC 27603', phone: '+1 (919) 555-0148' },
    ])
  })

  it('starts with one empty location row when none exist', () => {
    const draft = organizationToDraft({ ...sampleOrg, locations: [] })

    expect(draft.locations).toEqual([{ address: '', phone: '' }])
  })
})

describe('validateOrganizationDraft', () => {
  it('requires a name', () => {
    const errors = validateOrganizationDraft(
      { ...organizationToDraft(sampleOrg), name: '   ' },
      'Vendor',
    )

    expect(errors.name).toBe('Name is required.')
  })

  it('rejects an invalid contact email', () => {
    const errors = validateOrganizationDraft(
      { ...organizationToDraft(sampleOrg), primaryContactEmail: 'not-an-email' },
      'Vendor',
    )

    expect(errors.primaryContactEmail).toBe('Enter a valid email address.')
  })

  it('rejects preferred vendor on a client organization', () => {
    const errors = validateOrganizationDraft(
      { ...organizationToDraft(sampleOrg), isPreferredVendor: true },
      'Client',
    )

    expect(errors.isPreferredVendor).toBe('Only vendor organizations can be marked as preferred.')
  })

  it('accepts a blank primary contact phone, since it is optional', () => {
    const errors = validateOrganizationDraft(
      { ...organizationToDraft(sampleOrg), primaryContactPhone: '' },
      'Vendor',
    )

    expect(errors.primaryContactPhone).toBeUndefined()
  })

  it('rejects a primary contact phone with letters or too few digits', () => {
    const errors = validateOrganizationDraft(
      { ...organizationToDraft(sampleOrg), primaryContactPhone: 'call me maybe' },
      'Vendor',
    )

    expect(errors.primaryContactPhone).toBe('Enter a valid phone number.')
  })

  it('accepts a well-formed primary contact phone', () => {
    const errors = validateOrganizationDraft(
      { ...organizationToDraft(sampleOrg), primaryContactPhone: '+1 (704) 555-0198' },
      'Vendor',
    )

    expect(errors.primaryContactPhone).toBeUndefined()
  })

  it('rejects an invalid phone on a location row, keyed to that row only', () => {
    const errors = validateOrganizationDraft(
      {
        ...organizationToDraft(sampleOrg),
        locations: [
          { address: '910 Logistics Way', phone: '+1 (919) 555-0148' },
          { address: 'Second Site', phone: '123' },
        ],
      },
      'Vendor',
    )

    expect(errors[locationPhoneErrorKey(0)]).toBeUndefined()
    expect(errors[locationPhoneErrorKey(1)]).toBe('Enter a valid phone number.')
  })
})

describe('emptyOrganizationDraft', () => {
  it('starts blank with one empty location row, ready for the create form', () => {
    const draft = emptyOrganizationDraft()

    expect(draft.name).toBe('')
    expect(draft.isPreferredVendor).toBe(false)
    expect(draft.locations).toEqual([{ address: '', phone: '' }])
  })
})

describe('draftToLocationInputs', () => {
  it('drops blank addresses and omits blank phones', () => {
    const payload = draftToLocationInputs({
      ...organizationToDraft(sampleOrg),
      locations: [
        { address: ' 910 Logistics Way ', phone: ' 555-0100 ' },
        { address: '   ', phone: '+1 (000) 000-0000' },
        { address: 'Second Site', phone: '' },
      ],
    })

    expect(payload).toEqual([
      { address: '910 Logistics Way', phone: '555-0100' },
      { address: 'Second Site', phone: undefined },
    ])
  })
})
