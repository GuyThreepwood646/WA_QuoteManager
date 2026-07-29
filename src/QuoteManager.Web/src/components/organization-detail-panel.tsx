import { Check, Plus, Trash2 } from 'lucide-react'

import type { OrganizationListItem } from '@/api/types'
import { FieldError, FormField, FormFieldRow } from '@/components/form-field'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import {
  locationPhoneErrorKey,
  type OrganizationDraft,
  type OrganizationLocationDraft,
} from '@/lib/organization-validation'
import { type FieldErrors } from '@/lib/form-validation'

export function OrganizationContactSummary({ org }: { org: OrganizationListItem }) {
  if (!org.primaryContactName && !org.primaryContactEmail && !org.primaryContactPhone) {
    return <span className="text-sm text-muted-foreground">—</span>
  }

  return (
    <div className="text-sm">
      {org.primaryContactName && <p className="font-medium">{org.primaryContactName}</p>}
      {org.primaryContactEmail && <p className="text-muted-foreground">{org.primaryContactEmail}</p>}
      {org.primaryContactPhone && <p className="text-muted-foreground">{org.primaryContactPhone}</p>}
    </div>
  )
}

/**
 * The name/address/contact/preferred-vendor/locations field set shared by editing an existing
 * organization (the detail panel below) and creating a new one (NewOrganizationPage) - same
 * business fields, same validation, only the surrounding form chrome differs between the two.
 */
export function OrganizationProfileFields({
  idPrefix,
  draft,
  fieldErrors,
  isVendor,
  onDraftChange,
  onClearFieldError,
}: {
  idPrefix: string
  draft: OrganizationDraft
  fieldErrors: FieldErrors
  isVendor: boolean
  onDraftChange: (next: OrganizationDraft) => void
  onClearFieldError: (field: string) => void
}) {
  function updateLocation(index: number, field: keyof OrganizationLocationDraft, value: string) {
    const next = draft.locations.map((location, locationIndex) =>
      locationIndex === index ? { ...location, [field]: value } : location,
    )
    onDraftChange({ ...draft, locations: next })
  }

  function addLocation() {
    onDraftChange({ ...draft, locations: [...draft.locations, { address: '', phone: '' }] })
  }

  function removeLocation(index: number) {
    const next = draft.locations.filter((_, locationIndex) => locationIndex !== index)
    onDraftChange({ ...draft, locations: next.length > 0 ? next : [{ address: '', phone: '' }] })
  }

  // A row's two fields sit side by side, so if only one of them grows to show an error, that
  // field's input drops out of line with its neighbour. Reserving the same error-line height on
  // both sides (blank when a field has none) keeps every input in a row level with each other -
  // the same technique FormFieldRow already applies for input-only pairs; the phone/preferred-
  // vendor row is hand-rolled below because the preferred-vendor control isn't a plain input.
  const phoneRowHasError = Boolean(fieldErrors.primaryContactPhone) || Boolean(fieldErrors.isPreferredVendor)

  return (
    <div className="flex flex-col gap-4">
      <FormFieldRow
        fields={[
          {
            id: `${idPrefix}-name`,
            label: 'Name',
            error: fieldErrors.name,
            children: (
              <Input
                value={draft.name}
                onChange={(event) => {
                  onDraftChange({ ...draft, name: event.currentTarget.value })
                  onClearFieldError('name')
                }}
                maxLength={200}
                autoFocus
              />
            ),
          },
          {
            id: `${idPrefix}-primaryAddress`,
            label: 'Primary address',
            children: (
              <Input
                value={draft.primaryAddress}
                onChange={(event) => onDraftChange({ ...draft, primaryAddress: event.currentTarget.value })}
                maxLength={500}
              />
            ),
          },
        ]}
      />

      <FormFieldRow
        fields={[
          {
            id: `${idPrefix}-primaryContactName`,
            label: 'Primary contact name',
            children: (
              <Input
                value={draft.primaryContactName}
                onChange={(event) => onDraftChange({ ...draft, primaryContactName: event.currentTarget.value })}
                maxLength={200}
              />
            ),
          },
          {
            id: `${idPrefix}-primaryContactEmail`,
            label: 'Primary contact email',
            error: fieldErrors.primaryContactEmail,
            children: (
              <Input
                type="email"
                value={draft.primaryContactEmail}
                onChange={(event) => {
                  onDraftChange({ ...draft, primaryContactEmail: event.currentTarget.value })
                  onClearFieldError('primaryContactEmail')
                }}
                maxLength={320}
              />
            ),
          },
        ]}
      />

      {isVendor ? (
        <div className="grid grid-cols-2 gap-3">
          <div className="flex flex-col gap-2">
            <Label htmlFor={`${idPrefix}-primaryContactPhone`}>Primary contact phone</Label>
            {phoneRowHasError && <RowErrorSlot error={fieldErrors.primaryContactPhone} id={`${idPrefix}-primaryContactPhone-error`} />}
            <Input
              id={`${idPrefix}-primaryContactPhone`}
              type="tel"
              value={draft.primaryContactPhone}
              onChange={(event) => {
                onDraftChange({ ...draft, primaryContactPhone: event.currentTarget.value })
                onClearFieldError('primaryContactPhone')
              }}
              maxLength={50}
              aria-invalid={fieldErrors.primaryContactPhone ? true : undefined}
            />
          </div>

          <div className="flex flex-col gap-2">
            <Label htmlFor={`${idPrefix}-isPreferredVendor`}>Preferred vendor</Label>
            {phoneRowHasError && <RowErrorSlot error={fieldErrors.isPreferredVendor} id={`${idPrefix}-preferred-error`} />}
            <label className="flex h-9 items-center gap-2 text-sm">
              <input
                id={`${idPrefix}-isPreferredVendor`}
                type="checkbox"
                checked={draft.isPreferredVendor}
                onChange={(event) => {
                  onDraftChange({ ...draft, isPreferredVendor: event.currentTarget.checked })
                  onClearFieldError('isPreferredVendor')
                }}
                className="size-4 rounded border border-input accent-primary"
              />
              Mark as a preferred vendor
            </label>
          </div>
        </div>
      ) : (
        <FormField id={`${idPrefix}-primaryContactPhone`} label="Primary contact phone" error={fieldErrors.primaryContactPhone}>
          <Input
            type="tel"
            value={draft.primaryContactPhone}
            onChange={(event) => {
              onDraftChange({ ...draft, primaryContactPhone: event.currentTarget.value })
              onClearFieldError('primaryContactPhone')
            }}
            maxLength={50}
          />
        </FormField>
      )}

      <div className="flex flex-col gap-2">
        <Label>Locations</Label>
        <div className="flex flex-col gap-3">
          {draft.locations.map((location, index) => (
            <div key={`${idPrefix}-location-${index}`} className="flex flex-col gap-2 rounded-md border border-border/60 p-3">
              <div className="flex gap-2">
                <Input
                  value={location.address}
                  onChange={(event) => updateLocation(index, 'address', event.currentTarget.value)}
                  maxLength={500}
                  placeholder={`Location ${index + 1} address`}
                />
                <Button
                  type="button"
                  size="icon"
                  variant="outline"
                  aria-label={`Remove location ${index + 1}`}
                  disabled={draft.locations.length === 1}
                  onClick={() => removeLocation(index)}
                >
                  <Trash2 className="size-4" />
                </Button>
              </div>
              <Input
                type="tel"
                value={location.phone}
                onChange={(event) => {
                  updateLocation(index, 'phone', event.currentTarget.value)
                  onClearFieldError(locationPhoneErrorKey(index))
                }}
                maxLength={50}
                placeholder="Location phone"
                aria-invalid={fieldErrors[locationPhoneErrorKey(index)] ? true : undefined}
              />
              {fieldErrors[locationPhoneErrorKey(index)] && (
                <FieldError id={`${idPrefix}-location-${index}-phone-error`}>
                  {fieldErrors[locationPhoneErrorKey(index)]}
                </FieldError>
              )}
            </div>
          ))}
        </div>
        <Button type="button" size="sm" variant="outline" className="self-start" onClick={addLocation}>
          <Plus className="size-4" />
          Add location
        </Button>
      </div>
    </div>
  )
}

/** The same reserved-height blank-or-error slot `FormFieldRow` uses, for the one row it can't cover. */
function RowErrorSlot({ error, id }: { error?: string | null; id: string }) {
  return (
    <div className="min-h-5">
      {error ? (
        <FieldError id={id}>{error}</FieldError>
      ) : (
        <span className="invisible block text-sm leading-5" aria-hidden="true">
          &nbsp;
        </span>
      )}
    </div>
  )
}

export function OrganizationDetailPanel({
  org,
  isEditing,
  draft,
  fieldErrors,
  isSaving,
  isAdmin,
  onDraftChange,
  onClearFieldError,
  onEdit,
  onDiscard,
  onSave,
}: {
  org: OrganizationListItem
  isEditing: boolean
  draft: OrganizationDraft
  fieldErrors: FieldErrors
  isSaving: boolean
  isAdmin: boolean
  onDraftChange: (next: OrganizationDraft) => void
  onClearFieldError: (field: string) => void
  onEdit: () => void
  onDiscard: () => void
  onSave: () => void
}) {
  const isVendor = org.kind === 'Vendor'

  return (
    <div className="flex flex-col gap-4 rounded-md border border-border/60 bg-muted/20 p-4">
      <div className="flex items-center justify-between gap-3">
        <p className="text-sm font-semibold">Organization details</p>
        {isAdmin && !org.retiredAt && (
          <div className="flex gap-2">
            {isEditing ? (
              <>
                <Button size="sm" disabled={isSaving} onClick={onSave}>
                  {isSaving ? 'Saving…' : 'Save'}
                </Button>
                <Button size="sm" variant="outline" disabled={isSaving} onClick={onDiscard}>
                  Discard
                </Button>
              </>
            ) : (
              <Button size="sm" variant="outline" onClick={onEdit}>
                Edit
              </Button>
            )}
          </div>
        )}
      </div>

      {isEditing ? (
        <OrganizationProfileFields
          idPrefix={org.id}
          draft={draft}
          fieldErrors={fieldErrors}
          isVendor={isVendor}
          onDraftChange={onDraftChange}
          onClearFieldError={onClearFieldError}
        />
      ) : (
        <div className="grid gap-4 md:grid-cols-2">
          <DetailField label="Name" value={org.name} />
          <DetailField label="Primary address" value={org.primaryAddress} />
          <DetailField label="Primary contact name" value={org.primaryContactName} />
          <DetailField label="Primary contact email" value={org.primaryContactEmail} />
          <DetailField label="Primary contact phone" value={org.primaryContactPhone} />
          {isVendor && (
            <DetailField
              label="Preferred vendor"
              value={org.isPreferredVendor ? 'Yes' : 'No'}
            />
          )}
          <div className="flex flex-col gap-1 md:col-span-2">
            <p className="text-xs font-medium uppercase tracking-wide text-muted-foreground">Locations</p>
            {org.locations.length === 0 ? (
              <p className="text-sm text-muted-foreground">—</p>
            ) : (
              <ul className="flex flex-col gap-2 text-sm text-foreground">
                {org.locations.map((location) => (
                  <li key={location.id} className="rounded-md border border-border/60 px-3 py-2">
                    <p>{location.address}</p>
                    {location.phone && <p className="text-muted-foreground">{location.phone}</p>}
                  </li>
                ))}
              </ul>
            )}
          </div>
        </div>
      )}
    </div>
  )
}

function DetailField({ label, value }: { label: string; value: string | null | undefined }) {
  return (
    <div className="flex flex-col gap-1">
      <p className="text-xs font-medium uppercase tracking-wide text-muted-foreground">{label}</p>
      <p className="text-sm">{value?.trim() ? value : '—'}</p>
    </div>
  )
}

export function PreferredVendorMark({ isPreferred }: { isPreferred: boolean }) {
  if (!isPreferred) {
    return <span className="text-sm text-muted-foreground">—</span>
  }

  return (
    <span className="inline-flex items-center gap-1 text-sm text-success" title="Preferred vendor">
      <Check className="size-4" aria-hidden="true" />
      <span className="sr-only">Preferred vendor</span>
    </span>
  )
}
