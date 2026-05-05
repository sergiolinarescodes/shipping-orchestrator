import { Badge, Button, Dialog } from "@ship/ui";
import type { CustomerShipmentView } from "../types/api";

export interface PrintLabelsModalProps {
  open: boolean;
  shipments: CustomerShipmentView[];
  onClose: () => void;
}

export function PrintLabelsModal({ open, shipments, onClose }: PrintLabelsModalProps) {
  const labeled = shipments.filter((s) => s.trackingNumber);
  const labelsAvailable = labeled.length > 0;

  return (
    <Dialog
      open={open}
      onOpenChange={(v) => !v && onClose()}
      title="Print labels"
      description={
        labeled.length > 0
          ? `${labeled.length} label${labeled.length === 1 ? "" : "s"} ready for printing.`
          : "No labels are ready for this batch yet. Wait for the carrier connector to finish."
      }
      footer={
        <>
          <Button variant="ghost" size="sm" onClick={onClose}>
            Close
          </Button>
          <Button
            variant="primary"
            size="sm"
            disabled
            title="PDF generation is not enabled in this showcase build."
          >
            Print all
          </Button>
        </>
      }
    >
      <div className="rounded border border-amber-200 bg-amber-50 px-3 py-2 mb-3 text-[12px] text-amber-700">
        Label PDF generation is not enabled in this showcase build — the print stub is in place
        for future integration. Tracking numbers are real and already recorded against each shipment.
      </div>

      {labelsAvailable ? (
        <ul className="divide-y divide-border rounded border border-border">
          {labeled.map((s) => (
            <li key={s.shipmentId} className="flex items-center justify-between px-3 py-2.5 text-[12.5px]">
              <div className="flex items-center gap-2">
                <span className="font-mono text-ink-700">{s.trackingNumber}</span>
                <Badge variant="info">{s.carrierCode ?? "—"}</Badge>
              </div>
              <Button
                variant="ghost"
                size="sm"
                disabled
                title="PDF generation not enabled"
              >
                Print
              </Button>
            </li>
          ))}
        </ul>
      ) : (
        <div className="rounded border border-dashed border-border p-4 text-[12px] text-ink-500">
          The carrier connector is still working on this batch. Refresh the page in a moment.
        </div>
      )}
    </Dialog>
  );
}
