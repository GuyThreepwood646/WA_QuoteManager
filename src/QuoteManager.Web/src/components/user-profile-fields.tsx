import type { OrganizationListItem } from '@/api/types'
import { FieldError, FormFieldRow } from '@/components/form-field'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { type FieldErrors } from '@/lib/form-validation'
import { type UserDraft } from '@/lib/user-validation'

const ALL_ROLES = ['Requester', 'Reviewer', 'Vendor', 'Admin']

// Radix's Select can't represent "nothing selected" as a real, selectable item (an empty string
// value isn't accepted), so a distinct sentinel stands in for "no organization" and is translated
// back to '' - what the rest of the draft/validation/API layer actually uses - at the boundary.
const NO_ORGANIZATION = '__none__'

/**
 * The Email/DisplayName/Address/Phone field set shared by creating a new user (NewUserPage) and
 * editing an existing one (the admin table's in-place row, or a non-admin's own profile card) -
 * same business fields, same validation, only the surrounding form chrome differs. Roles and
 * Organization are only ever rendered (and only ever editable) when `isAdminEditing` is true - a
 * non-admin editing their own account never sees these two fields at all, since the backend
 * refuses any change to them from a non-admin regardless of what the UI would send.
 */
export function UserProfileFields({
  idPrefix,
  draft,
  fieldErrors,
  isAdminEditing,
  organizations,
  onDraftChange,
  onClearFieldError,
}: {
  idPrefix: string
  draft: UserDraft
  fieldErrors: FieldErrors
  isAdminEditing: boolean
  organizations: OrganizationListItem[]
  onDraftChange: (next: UserDraft) => void
  onClearFieldError: (field: string) => void
}) {
  function toggleRole(role: string, checked: boolean) {
    const nextRoles = checked ? [...draft.roles, role] : draft.roles.filter((r) => r !== role)
    onDraftChange({ ...draft, roles: nextRoles })
    onClearFieldError('roles')
    onClearFieldError('organizationId')
  }

  return (
    <div className="flex flex-col gap-4">
      <FormFieldRow
        fields={[
          {
            id: `${idPrefix}-displayName`,
            label: 'Display name',
            error: fieldErrors.displayName,
            children: (
              <Input
                value={draft.displayName}
                onChange={(event) => {
                  onDraftChange({ ...draft, displayName: event.currentTarget.value })
                  onClearFieldError('displayName')
                }}
                maxLength={200}
                autoFocus
              />
            ),
          },
          {
            id: `${idPrefix}-email`,
            label: 'Email',
            error: fieldErrors.email,
            children: (
              <Input
                type="email"
                value={draft.email}
                onChange={(event) => {
                  onDraftChange({ ...draft, email: event.currentTarget.value })
                  onClearFieldError('email')
                }}
                maxLength={256}
              />
            ),
          },
        ]}
      />

      <FormFieldRow
        fields={[
          {
            id: `${idPrefix}-address`,
            label: 'Address',
            children: (
              <Input
                value={draft.address}
                onChange={(event) => onDraftChange({ ...draft, address: event.currentTarget.value })}
                maxLength={500}
              />
            ),
          },
          {
            id: `${idPrefix}-phone`,
            label: 'Phone',
            error: fieldErrors.phone,
            children: (
              <Input
                type="tel"
                value={draft.phone}
                onChange={(event) => {
                  onDraftChange({ ...draft, phone: event.currentTarget.value })
                  onClearFieldError('phone')
                }}
                maxLength={50}
              />
            ),
          },
        ]}
      />

      {isAdminEditing && (
        <>
          <div className="flex flex-col gap-2">
            <Label>Roles</Label>
            {fieldErrors.roles && <FieldError id={`${idPrefix}-roles-error`}>{fieldErrors.roles}</FieldError>}
            <div className="flex flex-wrap gap-4">
              {ALL_ROLES.map((role) => (
                <label key={role} className="flex items-center gap-2 text-sm">
                  <input
                    type="checkbox"
                    checked={draft.roles.includes(role)}
                    onChange={(event) => toggleRole(role, event.currentTarget.checked)}
                    className="size-4 rounded border border-input accent-primary"
                  />
                  {role}
                </label>
              ))}
            </div>
          </div>

          <div className="flex flex-col gap-2">
            <Label htmlFor={`${idPrefix}-organizationId`}>Organization</Label>
            {fieldErrors.organizationId && (
              <FieldError id={`${idPrefix}-organizationId-error`}>{fieldErrors.organizationId}</FieldError>
            )}
            <Select
              value={draft.organizationId === '' ? NO_ORGANIZATION : draft.organizationId}
              onValueChange={(value) => {
                onDraftChange({ ...draft, organizationId: value === NO_ORGANIZATION ? '' : value })
                onClearFieldError('organizationId')
              }}
            >
              <SelectTrigger id={`${idPrefix}-organizationId`} className="w-full">
                <SelectValue placeholder="Select an organization" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value={NO_ORGANIZATION}>None (platform staff only)</SelectItem>
                {organizations.map((org) => (
                  <SelectItem key={org.id} value={org.id}>
                    {org.name}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
        </>
      )}
    </div>
  )
}
