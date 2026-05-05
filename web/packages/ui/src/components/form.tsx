import { type ReactNode } from "react";
import {
  type FieldValues,
  type SubmitHandler,
  type UseFormReturn,
  FormProvider,
  useFormContext,
} from "react-hook-form";
import { cn } from "../lib/cn";

export interface FormProps<TValues extends FieldValues> {
  form: UseFormReturn<TValues>;
  onSubmit: SubmitHandler<TValues>;
  className?: string;
  children: ReactNode;
}

export function Form<TValues extends FieldValues>({
  form,
  onSubmit,
  className,
  children,
}: FormProps<TValues>) {
  return (
    <FormProvider {...form}>
      <form
        onSubmit={form.handleSubmit(onSubmit)}
        className={cn("flex flex-col gap-4", className)}
        noValidate
      >
        {children}
      </form>
    </FormProvider>
  );
}

export interface FormFieldProps {
  name: string;
  label: string;
  description?: string;
  children: ReactNode;
  required?: boolean;
}

export function FormField({ name, label, description, children, required }: FormFieldProps) {
  const { formState: { errors } } = useFormContext();
  const error = errors[name];
  const errorMessage = typeof error?.message === "string" ? error.message : undefined;

  return (
    <div className="flex flex-col gap-1.5">
      <label
        htmlFor={name}
        className="block text-[12px] font-medium leading-snug text-ink-600"
      >
        {label} {required && <span className="text-red-500">*</span>}
      </label>
      {children}
      {description && !errorMessage && (
        <span className="text-[11px] text-ink-400">{description}</span>
      )}
      {errorMessage && (
        <span role="alert" className="text-[11px] text-red-600">
          {errorMessage}
        </span>
      )}
    </div>
  );
}
