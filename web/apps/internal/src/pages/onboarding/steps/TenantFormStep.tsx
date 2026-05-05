import { useForm } from "react-hook-form";
import { z } from "zod";
import { zodResolver } from "@hookform/resolvers/zod";
import { Button, Form, FormField, Input, Spinner } from "@ship/ui";
import type { StepRendererProps } from "../RendererRegistry";

const Schema = z.object({
  displayName: z.string().min(2, "Required").max(200),
  contactEmail: z.string().email("Must be a valid email").or(z.literal("")).optional(),
});

type FormValues = z.infer<typeof Schema>;

export function TenantFormStep({ step, onAdvance, isPending }: StepRendererProps) {
  const form = useForm<FormValues>({
    resolver: zodResolver(Schema),
    defaultValues: {
      displayName: "",
      contactEmail: "",
    },
  });

  const isCompleted = step.status === "Completed";

  return (
    <Form
      form={form}
      onSubmit={(values) =>
        onAdvance({
          displayName: values.displayName,
          contactEmail: values.contactEmail?.length ? values.contactEmail : null,
        })
      }
    >
      <FormField name="displayName" label="Tenant display name" required>
        <Input
          id="displayName"
          {...form.register("displayName")}
          disabled={isCompleted}
          placeholder="Acme NL"
        />
      </FormField>
      <FormField name="contactEmail" label="Contact email">
        <Input
          id="contactEmail"
          type="email"
          {...form.register("contactEmail")}
          disabled={isCompleted}
          placeholder="ops@acme.test"
        />
      </FormField>
      <div className="flex items-center gap-2 pt-1">
        <Button type="submit" variant="primary" size="md" disabled={isCompleted || isPending}>
          {isPending ? <Spinner /> : "Create tenant"}
        </Button>
        {step.failureReason && (
          <span className="text-[12px] text-red-600">{step.failureReason}</span>
        )}
      </div>
    </Form>
  );
}
