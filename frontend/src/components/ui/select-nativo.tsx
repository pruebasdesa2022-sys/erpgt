import type { ComponentProps } from "react";
import { cn } from "@/lib/utils";

/** Lista desplegable nativa con el mismo aspecto que los demás campos. */
function SelectNativo({ className, ...props }: ComponentProps<"select">) {
  return (
    <select
      data-slot="select-nativo"
      className={cn(
        "h-8 w-full min-w-0 rounded-lg border border-input bg-transparent px-2.5 py-1 text-sm transition-colors outline-none",
        "focus-visible:border-ring focus-visible:ring-3 focus-visible:ring-ring/50 disabled:cursor-not-allowed disabled:opacity-50",
        "aria-invalid:border-destructive aria-invalid:ring-3 aria-invalid:ring-destructive/20",
        className,
      )}
      {...props}
    />
  );
}

export { SelectNativo };
