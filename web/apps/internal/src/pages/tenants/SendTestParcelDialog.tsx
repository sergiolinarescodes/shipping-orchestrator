import { useNavigate } from "react-router-dom";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { zodResolver } from "@hookform/resolvers/zod";
import {
  Button,
  Dialog,
  Form,
  FormField,
  Input,
  Spinner,
  useToast,
} from "@ship/ui";
import { useSimulateOrderMutation } from "../../api/queries";

const Schema = z.object({
  originCountry: z.string().length(2).default("NL"),
  destinationCountry: z.string().length(2).default("NL"),
  weightGrams: z.coerce.number().int().min(50).max(20_000).default(1000),
  description: z.string().max(120).optional(),
});

type FormValues = z.infer<typeof Schema>;

export function SendTestParcelDialog({
  open,
  onOpenChange,
  tenantId,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  tenantId: string;
}) {
  const navigate = useNavigate();
  const { push } = useToast();
  const simulate = useSimulateOrderMutation(tenantId);
  const form = useForm<FormValues>({
    resolver: zodResolver(Schema),
    defaultValues: { originCountry: "NL", destinationCountry: "NL", weightGrams: 1000, description: "Acme widget" },
  });

  return (
    <Dialog
      open={open}
      onOpenChange={onOpenChange}
      title="Send a test parcel"
      description="Pushes a synthetic order through the same path a real Shopify webhook uses (ingest → batch → carrier label → tracking poll)."
    >
      <Form
        form={form}
        onSubmit={(values) => {
          simulate.mutate(values, {
            onSuccess: (resp) => {
              push({
                title: "Test parcel queued",
                description: `Batch ${resp.batchId.slice(0, 8)}… on the way.`,
                variant: "success",
              });
              onOpenChange(false);
              navigate(`/tenants/${tenantId}/batches/${resp.batchId}`);
            },
            onError: (err) => {
              push({
                title: "Could not send",
                description: err instanceof Error ? err.message : String(err),
                variant: "danger",
              });
            },
          });
        }}
      >
        <FormField name="originCountry" label="Origin country (ISO-2)">
          <Input id="originCountry" {...form.register("originCountry")} />
        </FormField>
        <FormField name="destinationCountry" label="Destination country (ISO-2)">
          <Input id="destinationCountry" {...form.register("destinationCountry")} />
        </FormField>
        <FormField name="weightGrams" label="Weight (grams)">
          <Input id="weightGrams" type="number" {...form.register("weightGrams")} />
        </FormField>
        <FormField name="description" label="Description">
          <Input id="description" {...form.register("description")} />
        </FormField>
        <div className="flex items-center justify-end gap-2 pt-2">
          <Button type="button" variant="ghost" size="md" onClick={() => onOpenChange(false)}>
            Cancel
          </Button>
          <Button type="submit" variant="primary" size="md" disabled={simulate.isPending}>
            {simulate.isPending ? <Spinner /> : "Send"}
          </Button>
        </div>
      </Form>
    </Dialog>
  );
}
