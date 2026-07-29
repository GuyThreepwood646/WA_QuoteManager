import * as React from 'react'

import { Label } from '@/components/ui/label'
import { cn } from '@/lib/utils'

export function fieldControlProps(id: string, error?: string | null) {
  const errorId = error ? `${id}-error` : undefined

  return {
    id,
    'aria-invalid': error ? true : undefined,
    'aria-describedby': errorId,
  } as const
}

export function FieldError({ id, children }: { id: string; children: React.ReactNode }) {
  return (
    <p id={id} role="alert" className="text-sm leading-5 text-destructive">
      {children}
    </p>
  )
}

type FormFieldRowItem = {
  id: string
  label: React.ReactNode
  error?: string | null
  children: React.ReactElement
}

/** Two fields on one row — when either has an error, both reserve the same error band so inputs stay aligned. */
export function FormFieldRow({
  fields,
  className,
}: {
  fields: [FormFieldRowItem, FormFieldRowItem]
  className?: string
}) {
  const rowHasError = fields.some((field) => Boolean(field.error))

  return (
    <div className={cn('grid grid-cols-2 gap-3', className)}>
      {fields.map((field) => {
        const errorId = field.error ? `${field.id}-error` : undefined

        return (
          <div key={field.id} className="flex flex-col gap-2">
            <Label htmlFor={field.id}>{field.label}</Label>
            {rowHasError && (
              <div className="min-h-5">
                {field.error && errorId ? (
                  <FieldError id={errorId}>{field.error}</FieldError>
                ) : (
                  <span className="invisible block text-sm leading-5" aria-hidden="true">
                    &nbsp;
                  </span>
                )}
              </div>
            )}
            {React.cloneElement(field.children, fieldControlProps(field.id, field.error))}
          </div>
        )
      })}
    </div>
  )
}

export function FormField({
  id,
  label,
  error,
  className,
  children,
}: {
  id: string
  label: React.ReactNode
  error?: string | null
  className?: string
  children: React.ReactElement
}) {
  const errorId = error ? `${id}-error` : undefined
  const controlProps = fieldControlProps(id, error)

  return (
    <div className={cn('flex flex-col gap-2', className)}>
      <Label htmlFor={id}>{label}</Label>
      {error && errorId && <FieldError id={errorId}>{error}</FieldError>}
      {React.cloneElement(children, controlProps)}
    </div>
  )
}

export const textareaClassName = cn(
  'min-h-20 w-full rounded-md border border-input bg-transparent px-3 py-2 text-base shadow-xs transition-[color,box-shadow] outline-none selection:bg-primary selection:text-primary-foreground placeholder:text-muted-foreground disabled:pointer-events-none disabled:cursor-not-allowed disabled:opacity-50 md:text-sm dark:bg-input/30',
  'focus-visible:border-ring focus-visible:ring-[3px] focus-visible:ring-ring/50',
  'aria-invalid:border-destructive aria-invalid:ring-destructive/20 dark:aria-invalid:ring-destructive/40',
)
