import { Check, X } from 'lucide-react'

import { PASSWORD_REQUIREMENTS } from '@/lib/password-validation'

/**
 * The live red-X/green-check checklist shown wherever a new password is entered - the create-user
 * form and both reset-password forms (self and admin-on-behalf) all render this same component
 * against their own password field's current value.
 */
export function PasswordRequirements({ password }: { password: string }) {
  return (
    <ul className="flex flex-col gap-1">
      {PASSWORD_REQUIREMENTS.map((requirement) => {
        const met = requirement.test(password)
        return (
          <li key={requirement.id} className="flex items-center gap-2 text-sm">
            {met ? (
              <Check className="size-4 shrink-0 text-success" aria-hidden="true" />
            ) : (
              <X className="size-4 shrink-0 text-destructive" aria-hidden="true" />
            )}
            <span className={met ? 'text-muted-foreground' : 'text-foreground'}>{requirement.label}</span>
          </li>
        )
      })}
    </ul>
  )
}
