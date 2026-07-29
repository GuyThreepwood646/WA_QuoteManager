import * as React from "react"

import { cn } from "@/lib/utils"

// Browsers only open a date/time picker when the calendar icon itself is clicked. Opening it on
// any click within the field is what a user expects from a "click to pick a date" control.
const PICKER_INPUT_TYPES = new Set(["date", "time", "datetime-local", "month", "week"])

function Input({ className, type, onClick, ...props }: React.ComponentProps<"input">) {
  const handleClick = (event: React.MouseEvent<HTMLInputElement>) => {
    onClick?.(event)

    const input = event.currentTarget
    if (type && PICKER_INPUT_TYPES.has(type) && !input.disabled && !input.readOnly) {
      try {
        input.showPicker?.()
      } catch {
        // Unsupported in this browser, or not triggered by a direct user gesture - fall back to
        // the browser's default click behaviour.
      }
    }
  }

  return (
    <input
      type={type}
      onClick={handleClick}
      data-slot="input"
      className={cn(
        "h-9 w-full min-w-0 rounded-md border border-input bg-transparent px-3 py-1 text-base shadow-xs transition-[color,box-shadow] outline-none selection:bg-primary selection:text-primary-foreground file:inline-flex file:h-7 file:border-0 file:bg-transparent file:text-sm file:font-medium file:text-foreground placeholder:text-muted-foreground disabled:pointer-events-none disabled:cursor-not-allowed disabled:opacity-50 md:text-sm dark:bg-input/30",
        "focus-visible:border-ring focus-visible:ring-[3px] focus-visible:ring-ring/50",
        "aria-invalid:border-destructive aria-invalid:ring-destructive/20 dark:aria-invalid:ring-destructive/40",
        className
      )}
      {...props}
    />
  )
}

export { Input }
